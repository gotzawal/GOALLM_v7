# https://github.com/AIRI-Institute/AriGraph/tree/main

import os
import re
import ast
import json
import numpy as np

import re
from time import time
from copy import deepcopy
import json
import requests
from time import time, sleep
from openai import OpenAI

# from utils.utils import clear_triplet, check_conn, find_relation
# (또한 prompt_extraction_current, prompt_refining_items, process_triplets, parse_triplets_removing,
#  graph_retr_search, find_top_episodic_emb, top_k_obs 등 필요한 함수 및 상수들이 이미 정의되었다고 가정)
from retriever import Retriever, graph_retr_search, find_top_episodic_emb
#from utils import clear_triplet, process_triplets, parse_triplets_removing, top_k_obs
from prompt import prompt_refining_items, prompt_extraction_current

class ContrieverGraph:
    def __init__(self, model, system_prompt, api_key, device="cpu", debug=False):
        self.triplets = []
        self.items = []  # items 목록 초기화
        self.model, self.system_prompt = model, system_prompt
        self.debug = debug  # 디버그 모드 플래그
        self.client = OpenAI(
            api_key=api_key,
        )
        self.total_amount = 0

        self.retriever = Retriever(device)
        self.triplets_emb, self.items_emb = {}, {}
        self.obs_episodic, self.obs_episodic_list, self.top_episodic_dict_list = {}, [], []

    def clear(self):
        self.triplets = []
        self.total_amount = 0
        self.triplets_emb, self.items_emb = {}, {}
        self.obs_episodic, self.obs_episodic_list, self.top_episodic_dict_list = {}, [], []

    def generate(self, prompt, jsn=False, t=0.7):
        if jsn:
            chat_completion = self.client.chat.completions.create(
                messages=[
                    {"role": "system", "content": self.system_prompt},
                    {"role": "user", "content": prompt}
                ],
                model=self.model,
                response_format={"type": "json_object"},
                temperature=t
            )
        else:
            chat_completion = self.client.chat.completions.create(
                messages=[
                    {"role": "system", "content": self.system_prompt},
                    {"role": "user", "content": prompt}
                ],
                model=self.model,
                temperature=t
            )
        response = chat_completion.choices[0].message.content
        prompt_tokens = chat_completion.usage.prompt_tokens
        completion_tokens = chat_completion.usage.completion_tokens

        cost = completion_tokens * 3 / 100000 + prompt_tokens * 1 / 100000
        self.total_amount += cost
        return response, cost

    def str(self, triplet):
        return triplet[0] + ", " + triplet[2]["label"] + ", " + triplet[1]

    def triplets_to_str(self, triplets):
        return [self.str(triplet) for triplet in triplets]

    def convert(self, triplets):
        new_triplets = []
        for triplet in triplets:
            triplet = clear_triplet(triplet)
            new_triplets.append(self.str(triplet))
        return new_triplets

    def get_embedding_local(self, text):
        return self.retriever.embed([text])[0].cpu().detach().numpy()

    def add_triplets(self, triplets):
        for triplet in triplets:
            # 예시: label이 'free'이면 추가하지 않음
            if triplet[2]["label"] == "free":
                continue
            triplet = clear_triplet(triplet)
            if triplet not in self.triplets:
                self.triplets.append(triplet)
                self.triplets_emb[self.str(triplet)] = self.get_embedding_local(self.str(triplet))
                if triplet[0] not in self.items_emb:
                    self.items_emb[triplet[0]] = self.get_embedding_local(triplet[0])
                if triplet[1] not in self.items_emb:
                    self.items_emb[triplet[1]] = self.get_embedding_local(triplet[1])

    def delete_triplets(self, triplets, locations):
        for triplet in triplets:
            if triplet[0] in locations and triplet[1] in locations:
                continue
            if triplet in self.triplets:
                self.triplets.remove(triplet)
                self.triplets_emb.pop(self.str(triplet), None)

    def exclude(self, triplets):
        new_triplets = []
        for triplet in triplets:
            triplet = clear_triplet(triplet)
            if triplet not in self.triplets:
                new_triplets.append(triplet)
        return new_triplets

    def get_associated_triplets(self, items, steps=2):
        items = deepcopy([string.lower() for string in items])
        associated_triplets = []
        for i in range(steps):
            now = set()
            for triplet in self.triplets:
                for item in items:
                    if (item == triplet[0] or item == triplet[1]) and self.str(triplet) not in associated_triplets:
                        associated_triplets.append(self.str(triplet))
                        if item == triplet[0]:
                            now.add(triplet[1])
                        if item == triplet[1]:
                            now.add(triplet[0])
                        break
            if "itself" in now:
                now.remove("itself")
            items = now
        return associated_triplets

    # --------------------------------------------------------------------
    # update_without_retrieve: 트리플릿 추출/정제/추가 및 episodic memory 업데이트
    # --------------------------------------------------------------------
    def update_without_retrieve(self, observation, plan, prev_subgraph, locations, action, items1, log):
        #overall_start = time.time()
        if self.debug:
            print("=== DEBUG: 시작 update_without_retrieve ===")

        # 1. 트리플릿 추출 및 처리
        #t0 = time.time()
        example = [re.sub(r"Step \d+: ", "", triplet) for triplet in prev_subgraph]
        prompt = prompt_extraction_current.format(observation=observation, example=example)
        #if self.debug:
        #    print(f"[Extraction] 프롬프트 생성 시간: {time.time() - t0:.4f} sec")

        #t1 = time.time()
        response, _ = self.generate(prompt, t=0.1)
        #if self.debug:
        #    print(f"[Extraction] generate 호출 시간: {time.time() - t1:.4f} sec")

        #t2 = time.time()
        new_triplets_raw = process_triplets(response)
        #if self.debug:
        #    print(f"[Extraction] 트리플릿 파싱 시간: {time.time() - t2:.4f} sec")

        #t3 = time.time()
        new_triplets = self.exclude(new_triplets_raw)
        #if self.debug:
        #    print(f"[Extraction] 제외 및 변환 시간: {time.time() - t3:.4f} sec")

        log("New triplets: " + str(self.convert(new_triplets_raw)))

        # 2. 아이템 정제 및 기존 트리플릿 삭제
        #t4 = time.time()
        items_extracted = {triplet[0] for triplet in new_triplets_raw} | {triplet[1] for triplet in new_triplets_raw}
        associated_subgraph = self.get_associated_triplets(items_extracted, steps=1)
        prompt_refine = prompt_refining_items.format(ex_triplets=associated_subgraph, new_triplets=self.convert(new_triplets_raw))
        response_refine, _ = self.generate(prompt_refine, t=0.001)
        predicted_outdated = parse_triplets_removing(response_refine)
        self.delete_triplets(predicted_outdated, locations)
        #if self.debug:
        #    print(f"[Refinement] 정제 및 삭제 시간: {time.time() - t4:.4f} sec")
        log("Outdated triplets: " + response_refine)
        log("NUMBER OF REPLACEMENTS: " + str(len(predicted_outdated)))

        # 3. 새로운 트리플릿 추가
        #t5 = time.time()
        self.add_triplets(new_triplets_raw)
        #if self.debug:
        #    print(f"[Add Triplets] 추가 시간: {time.time() - t5:.4f} sec")

        # 4. plan의 context를 episodic memory에 저장 (중복 추가 방지)
        #t6 = time.time()
        try:
            plan_dict = json.loads(plan)
            context_info = plan_dict.get("context_info", "")
        except Exception as e:
            context_info = ""
        if context_info and context_info not in self.obs_episodic:
            context_embedding = self.retriever.embed(context_info)
            recent_triplets_str = self.triplets_to_str(self.triplets[-5:])  # 최근 5개의 트리플릿 사용
            context_value = [recent_triplets_str, context_embedding]
            self.obs_episodic[context_info] = context_value
        #if self.debug:
        #    print(f"[Final] plan context 임베딩 및 업데이트 시간: {time.time() - t6:.4f} sec")

        #overall_time = time.time() - overall_start
        #if self.debug:
        #    print(f"=== DEBUG: 전체 update_without_retrieve 소요 시간: {overall_time:.4f} sec ===")

    # --------------------------------------------------------------------
    # memory_retrieve: 최근 추가된 트리플릿과 현재 observation 기반 검색
    # --------------------------------------------------------------------
    def memory_retrieve(self, observation, plan, prev_subgraph, recent_n=5, topk_episodic=2):
        #overall_start = time.time()
        if self.debug:
            print("=== DEBUG: 시작 memory_retrieve ===")

        # 1. 최근 n개의 트리플릿 기반 연관 서브그래프 재계산
        #t0 = time.time()
        triplets_str = self.triplets_to_str(self.triplets)  # 전체 트리플릿 목록(문자열)
        associated_subgraph_new = set()
        recent_triplets = self.triplets[-recent_n:]
        recent_triplets_str = self.triplets_to_str(recent_triplets)
        for trip in recent_triplets_str:
            results = graph_retr_search(
                trip, triplets_str, self.retriever,
                max_depth=3,
                topk=4,
                post_retrieve_threshold=0.65,
                verbose=2
            )
            associated_subgraph_new.update(results)
        # 최근 추가된 트리플릿은 제외
        associated_subgraph_new = [element for element in associated_subgraph_new if element not in recent_triplets_str]
        #if self.debug:
        #    print(f"[Retrieval] 최근 {recent_n}개 트리플릿 기반 연관 서브그래프 계산 시간: {time.time() - t0:.4f} sec")

        # 2. Episodic memory 검색 (현재 observation 기반)
        #t1 = time.time()
        observation_embedding = self.retriever.embed(observation)
        top_episodic_dict = find_top_episodic_emb(prev_subgraph, deepcopy(self.obs_episodic), observation_embedding, self.retriever)
        top_episodic = top_k_obs(top_episodic_dict, k=topk_episodic)
        #if self.debug:
        #    print(f"[Episodic] top episodic 계산 시간: {time.time() - t1:.4f} sec")

        #overall_time = time.time() - overall_start
        #if self.debug:
        #    print(f"=== DEBUG: 전체 memory_retrieve 소요 시간: {overall_time:.4f} sec ===")
        return associated_subgraph_new, top_episodic




def clear_triplet(triplet):
    return [triplet[0].lower().strip('''"'. `;:'''),
            triplet[1].lower().strip('''"'. `;:'''),
            {"label": triplet[2]["label"].lower().strip('''"'. `;:''')}]

def process_triplets(raw_triplets):
    raw_triplets = raw_triplets.split(";")
    triplets = []
    for triplet in raw_triplets:
        if len(triplet.split(",")) != 3:
            continue
        if triplet[0] in "123456789":
            triplet = triplet[2:]
        subj, relation, obj = triplet.split(",")
        subj = subj.split(":")[-1].strip(''' '\n"''')
        relation = relation.strip(''' '\n"''')
        obj = obj.strip(''' '\n"''')
        if len(subj) == 0 or len(relation) == 0 or len(obj) == 0:
            continue
        triplets.append([subj, obj, {"label": relation}])
    return triplets


def parse_triplets_removing(text):
    text = text.split("[[")[-1] if "[[" in text else text.split("[\n[")[-1]
    text = text.replace("[", "")
    text = text.strip("]")
    pairs = text.split("],")
    parsed_triplets = []
    for pair in pairs:
        splitted_pair = pair.split("->")
        if len(splitted_pair) != 2:
            continue
        first_triplet = splitted_pair[0].split(",")
        if len(first_triplet) != 3:
            continue
        subj = first_triplet[0].strip(''' '"\n''')
        rel = first_triplet[1].strip(''' '"\n''')
        obj = first_triplet[2].strip(''' '"\n''')
        parsed_triplets.append([subj, obj, {"label": rel}])
    return parsed_triplets


def top_k_obs(input_dict, k):
    # 각 키의 리스트 값의 합계를 계산
    sum_dict = {key: sum(values) for key, values in input_dict.items()}
    # 합계를 기준으로 내림차순 정렬 후 상위 k개 키 반환
    sorted_keys = sorted(sum_dict, key=sum_dict.get, reverse=True)
    return sorted_keys[:k]
