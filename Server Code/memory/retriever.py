# https://github.com/AIRI-Institute/AriGraph/tree/main

import torch
import torch.nn.functional as F
from collections import OrderedDict
import numpy as np
from sentence_transformers import SentenceTransformer, models

# 전역 캐시 (LRU 방식)
embedding_cache = OrderedDict()
CACHE_SIZE = 100  # 캐싱할 최대 임베딩 개수

# 각 모델별 설정 (Hugging Face Model Hub 기준)
MODEL_CONFIGS = {
    "paraphrase-multilingual-mpnet-base-v2": {
        "load_direct": True,
        "model_name": "paraphrase-multilingual-mpnet-base-v2"
    },
    "LaBSE": {
        "load_direct": True,
        "model_name": "sentence-transformers/LaBSE"
    },
    "multilingual-e5-large-instruct": {
        "load_direct": False,
        "model_name": "intfloat/multilingual-e5-large",
        "pooling": "mean",         # 평균 풀링 사용
        "max_seq_length": 512      # 최대 시퀀스 길이
    },
    "BGE-M3": {
        "load_direct": False,
        "model_name": "BAAI/bge-m3",
        "pooling": "cls",          # CLS 토큰 풀링 사용
        "max_seq_length": 8192
    },
    "Nomic-Embed": {
        "load_direct": False,
        "model_name": "nomic-ai/nomic-embed-text-v1",
        "pooling": "mean",         # 평균 풀링 사용
        "max_seq_length": 8192
    }
}

class Retriever:
    """
    Sentence-Transformer 기반 임베딩 생성 및 코사인 유사도 계산을 통해 결과를 리턴하는 클래스.

    모델은 MODEL_CONFIGS의 설정에 따라 Hugging Face Transformer와 Pooling을 조합하여 로드됩니다.
    """
    def __init__(self, device='cpu', model_key='paraphrase-multilingual-mpnet-base-v2'):
        self.device = device
        config = MODEL_CONFIGS.get(model_key)
        if config is None:
            raise ValueError(f"Model key '{model_key}' is not defined in MODEL_CONFIGS.")
        if config.get("load_direct", False):
            # 이미 SentenceTransformer 형식인 모델이면 바로 로드
            self.embedder = SentenceTransformer(config["model_name"], device=device)
        else:
            # Transformer + Pooling 조합으로 SentenceTransformer 구성
            transformer = models.Transformer(
                model_name_or_path=config["model_name"],
                max_seq_length=config.get("max_seq_length", 256),
                device=device
            )
            pooling_mode = config.get("pooling", "mean")
            if pooling_mode == "mean":
                pooling = models.Pooling(
                    transformer.get_word_embedding_dimension(),
                    pooling_mode_mean_tokens=True,
                    pooling_mode_cls_token=False
                )
            elif pooling_mode == "cls":
                pooling = models.Pooling(
                    transformer.get_word_embedding_dimension(),
                    pooling_mode_cls_token=True
                )
            else:
                pooling = models.Pooling(
                    transformer.get_word_embedding_dimension(),
                    pooling_mode_mean_tokens=True,
                    pooling_mode_cls_token=False
                )
            self.embedder = SentenceTransformer(modules=[transformer, pooling])

    def embed(self, texts):
        """
        주어진 문자열 리스트를 임베딩 텐서로 변환합니다.
        :param texts: list[str]
        :return: torch.Tensor, shape: (num_texts, embed_dim)
        """
        embeddings = self.embedder.encode(texts, convert_to_tensor=True, device=self.device)
        return embeddings

    @torch.no_grad()
    def search_in_embeds(self, key_embeds, query_embeds, topk: int = None, similarity_threshold: float = None,
                         return_embeds: bool = False, return_scores: bool = False):
        """
        key_embeds와 query_embeds 간의 코사인 유사도를 계산하여,
        topk 또는 similarity_threshold 조건에 맞는 결과를 반환합니다.
        """
        if int(topk is None) + int(similarity_threshold is None) != 1:
            raise ValueError("You should specify either topk or similarity_threshold but not both!")

        query_embeds_norm = F.normalize(query_embeds, p=2, dim=-1)
        key_embeds_norm = F.normalize(key_embeds, p=2, dim=-1)
        scores = query_embeds_norm @ key_embeds_norm.transpose(-1, -2)  # shape: (M, N)

        batch_request = len(query_embeds.shape) > 1
        if not batch_request:
            scores = scores.reshape(1, -1)
        num_q = scores.shape[0]

        if topk is not None:
            sorted_idx = scores.argsort(dim=-1, descending=True)
            selected_idx = sorted_idx[:, :topk]
            selected_idx = selected_idx.tolist()
        else:
            selected_idx = [[] for _ in range(num_q)]
            nonzero_indices = (scores >= similarity_threshold).nonzero(as_tuple=False)
            for item in nonzero_indices:
                q_id, k_id = item[0].item(), item[1].item()
                selected_idx[q_id].append(k_id)

        result = {'idx': selected_idx}
        if return_embeds:
            result['embeds'] = [
                [key_embeds[k_id] for k_id in selected_idx[q_id]]
                for q_id in range(num_q)
            ]
        if return_scores:
            result['scores'] = [
                [scores[q_id, k_id].item() for k_id in selected_idx[q_id]]
                for q_id in range(num_q)
            ]
        if not batch_request:
            result = {k: v[0] for k, v in result.items()}
        return result

def sort_scores(data):
    """
    data['idx']와 data['scores']가 단일 값일 경우에도 리스트 형태로 다룰 수 있도록 보정합니다.
    각 쿼리에 대해 인덱스와 점수를 인덱스 기준 오름차순으로 정렬합니다.
    """
    idx_data = data['idx']
    score_data = data['scores']

    # 단일 값이면 리스트로 감싸기
    if not isinstance(idx_data, list):
        idx_data = [idx_data]
    if not isinstance(score_data, list):
        score_data = [score_data]
    # 만약 내부가 단일 int/float라면 한 번 더 리스트로 감싸기
    if idx_data and not isinstance(idx_data[0], list):
        idx_data = [idx_data]
    if score_data and not isinstance(score_data[0], list):
        score_data = [score_data]

    sorted_idx_all = []
    sorted_scores_all = []
    for idx_list, score_list in zip(idx_data, score_data):
        paired = list(zip(idx_list, score_list))
        paired_sorted = sorted(paired, key=lambda x: x[0])
        if paired_sorted:
            idx_sorted, score_sorted = zip(*paired_sorted)
        else:
            idx_sorted, score_sorted = [], []
        sorted_idx_all.append(list(idx_sorted))
        sorted_scores_all.append(list(score_sorted))
    return {'idx': sorted_idx_all, 'scores': sorted_scores_all}

def get_cached_embeddings(texts, retriever):
    """
    주어진 문자열 리스트에 대해 캐시된 임베딩이 있으면 사용하고,
    없는 경우 Retriever.embed()를 통해 계산 후 캐싱합니다.
    """
    results = [None] * len(texts)
    texts_to_compute = []
    indices_to_compute = []
    for i, text in enumerate(texts):
        if text in embedding_cache:
            embedding_cache.move_to_end(text)
            results[i] = embedding_cache[text]
        else:
            texts_to_compute.append(text)
            indices_to_compute.append(i)
    if texts_to_compute:
        computed = retriever.embed(texts_to_compute)
        for idx, text, emb in zip(indices_to_compute, texts_to_compute, computed):
            results[idx] = emb
            embedding_cache[text] = emb
            if len(embedding_cache) > CACHE_SIZE:
                embedding_cache.popitem(last=False)
    return torch.stack(results)


@torch.no_grad()
def graph_retr_search(start_triplet, triplets, retriever, max_depth: int = 2,
                      topk: int = 3, post_retrieve_threshold: float = 0.7,
                      verbose: int = 2):
    """
    시작 쿼리(triplet)를 기반으로 주어진 triplets에서 BFS 방식으로 관련 결과를 탐색합니다.
    """
    key_embeds = get_cached_embeddings(triplets, retriever)
    current_level = [start_triplet]  # 탐색 시작 쿼리 리스트
    depth = {start_triplet: 0}         # 각 트리플릿의 탐색 깊이 기록
    result = set()                   # 최종 검색된 트리플릿 집합
    visited = set([start_triplet])   # 중복 검색 방지를 위한 집합

    while current_level:
        query_embeds = get_cached_embeddings(current_level, retriever)
        if query_embeds.ndim == 1:
            query_embeds = query_embeds.unsqueeze(0)
        query_embeds_norm = F.normalize(query_embeds, p=2, dim=-1)
        key_embeds_norm = F.normalize(key_embeds, p=2, dim=-1)
        scores = query_embeds_norm @ key_embeds_norm.transpose(-1, -2)
        effective_topk = min(topk, scores.shape[1])
        topk_values, topk_indices = scores.topk(effective_topk, dim=1)
        next_level = []
        for i, query in enumerate(current_level):
            current_depth = depth[query]
            for score, idx in zip(topk_values[i].tolist(), topk_indices[i].tolist()):
                if score < post_retrieve_threshold:
                    continue
                candidate_triplet = triplets[idx]
                if candidate_triplet in visited:
                    continue
                result.add(candidate_triplet)
                if current_depth < max_depth:
                    next_level.append(candidate_triplet)
                    depth[candidate_triplet] = current_depth + 1
                    visited.add(candidate_triplet)
        current_level = next_level
    return list(result)

def find_top_episodic_emb(A, B, obs_plan_embedding, retriever):
    results = {}
    if not B:
        return results
    # B 딕셔너리의 각 value[1]을 스택하여 (N, embed_dim) 텐서 생성
    key_embeddings = torch.stack([value[1] for value in B.values()])

    # obs_plan_embedding이 1차원일 경우 2차원으로 변경
    if obs_plan_embedding.ndim == 1:
        obs_plan_embedding = obs_plan_embedding.unsqueeze(0)

    # Retriever를 이용해 모든 항목에 대해 유사도 계산 (topk로 모든 결과 반환)
    similarity_results = retriever.search_in_embeds(
        key_embeds=key_embeddings,
        query_embeds=obs_plan_embedding,
        topk=len(B),
        return_scores=True
    )

    similarity_results = sort_scores(similarity_results)

    # similarity_scores가 중첩된 리스트라면 플래튼 처리
    if similarity_results.get('scores'):
        similarity_scores = similarity_results['scores']
        if isinstance(similarity_scores[0], list):
            similarity_scores = similarity_scores[0]
    else:
        similarity_scores = [0] * len(B)

    max_similarity_score = max(similarity_scores, default=0)
    # max_similarity_score가 0이 아닌 경우 각 스코어를 정규화
    similarity_scores = [score / max_similarity_score if max_similarity_score else 0 for score in similarity_scores]

    # A의 요소와 B의 각 value_list 간의 매칭 횟수를 계산
    match_counts = [sum(1 for element in A if element in value_list) for _, (value_list, _) in B.items()]

    match_counts_relative = []
    for i, values in enumerate(B.values()):
        # values[0]는 에피소드의 요소 리스트라고 가정합니다.
        match_counts_relative.append((match_counts[i] / (len(values[0]) + 1e-9)) * np.log((len(values[0]) + 1e-9)))

    max_match_count = max(match_counts_relative, default=0)
    normalized_match_scores = [count / max_match_count if max_match_count else 0 for count in match_counts_relative]

    # 결과 딕셔너리에 각 에피소드의 정규화된 match score와 similarity score 할당
    for idx, (key, _) in enumerate(B.items()):
        results[key] = [normalized_match_scores[idx], similarity_scores[idx]]
    return results


def filter_items_by_similarity(data, query, threshold, retriever, max_n):
    """
    각 (주제, 내용) 항목을 "주제: 내용" 문자열로 결합하여 임베딩한 뒤,
    query와의 코사인 유사도가 threshold 이상인 항목을 (주제, 내용, score) 형태로 반환합니다.
    최대 max_n개 항목만 반환합니다.
    """
    texts = [f"{subject}: {content}" for subject, content in data]
    key_embeds = get_cached_embeddings(texts, retriever)
    query_embed = get_cached_embeddings([query], retriever)
    key_embeds_norm = F.normalize(key_embeds, p=2, dim=-1)
    query_embed_norm = F.normalize(query_embed, p=2, dim=-1)
    similarities = (key_embeds_norm @ query_embed_norm.transpose(-1, -2)).squeeze(1).cpu().numpy()
    filtered_items = []
    for i, score in enumerate(similarities):
        if score >= threshold:
            filtered_items.append((data[i][0], data[i][1], score))
    filtered_items = sorted(filtered_items, key=lambda x: x[2], reverse=True)
    return filtered_items[:max_n]

# ==== 예제 테스트 코드 ====
if __name__ == "__main__":
    # Retriever 인스턴스 생성: 모델 키는 원하시는 대로 선택하세요.
    # 여기서는 기본값인 'paraphrase-multilingual-mpnet-base-v2'를 사용합니다.
    retriever = Retriever(device='cpu',model_key='paraphrase-multilingual-mpnet-base-v2')  # 다른 모델: model_key='LaBSE', 'multilingual-e5-large-instruct', 'BGE-M3', 'Nomic-Embed'

    # --- 1. find_top_episodic_emb 예제 ---
    print("=== find_top_episodic_emb 예제 ===")
    obs_plan_text = ["The user is at the conference barracks"]
    obs_plan_embedding = retriever.embed(obs_plan_text)
    A = ["user", "conference", "barracks"]
    B = {
        "episode_1": (["user", "conference", "room"], retriever.embed(["User attended a meeting in the conference room"])[0]),
        "episode_2": (["user", "barracks", "sleeping"], retriever.embed(["User was sleeping in the barracks"])[0]),
        "episode_3": (["user", "cafeteria", "eating"], retriever.embed(["User was eating in the cafeteria"])[0])
    }
    episodic_results = find_top_episodic_emb(A, B, obs_plan_embedding, retriever)
    for episode, scores in episodic_results.items():
        match_score, similarity_score = scores
        print(f"{episode}: match_score={match_score:.3f}, similarity_score={similarity_score:.3f}")

    # --- 2. graph_retr_search 예제 ---
    print("\n=== graph_retr_search 예제 ===")
    triplets = [
        "apple, is a, fruit",
        "fruit, can be, eaten",
        "banana, is a, fruit",
        "apple, is related to, banana",
        "orange, is a, fruit",
    ]
    start_triplet = "apple, is a, fruit"
    graph_results = graph_retr_search(start_triplet, triplets, retriever,
                                      max_depth=3, topk=3, post_retrieve_threshold=0.55, verbose=1)
    for r in graph_results:
        print(r)

    # --- 3. filter_items_by_similarity 예제 ---
    print("\n=== filter_items_by_similarity 예제 ===")
    data = [
        ("스포츠", "축구는 전 세계적으로 사랑받는 스포츠입니다."),
        ("기술", "인공지능은 현대 기술 발전의 핵심 동력입니다."),
        ("요리", "다양한 파스타 요리 레시피를 소개합니다."),
        ("과학", "양자 물리학은 미시 세계를 탐구합니다."),
        ("AI", "인공지능은 인공적인 지능입니다")
    ]
    query = "AI와 머신러닝의 최신 동향"
    threshold = 0.4
    filtered = filter_items_by_similarity(data, query, threshold, retriever, max_n=3)
    print("임계치 이상의 항목:")
    for item in filtered:
        print(item)
