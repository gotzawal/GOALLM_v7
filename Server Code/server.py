api_key = ''
# https://github.com/AIRI-Institute/AriGraph/tree/main

import json
import torch
import torch.nn.functional as F
from time import time
import getpass
from collections import OrderedDict
import time
import uuid
import logging
import traceback

from flask import Flask, request, jsonify
from flask_cors import CORS
from pyngrok import ngrok


# (GPTagent, ContrieverGraph, Retriever, get_cached_embeddings, 그리고 system_prompt 등 필요한 모듈/상수들은 이미 정의되었다고 가정)
from agent import GPTagent
from system_prompt import default_system_prompt, system_plan_agent, system_action_agent, completed_plan_prompt, exception_plan_prompt, turn_count_plan_prompt,system_status_agent, predefined_knowledge, energy_prompts, trust_prompts, status_prompts
from memory.arigraph import ContrieverGraph
from memory.graph_plot import plot_contriever_graph
from memory.retriever import filter_items_by_similarity, Retriever
from tts import generate_tts_audio

def Logger(log_file):
    def log_func(msg):
        print(msg)
    return log_func

log_file = "goallm_v7"
default_model = "gpt-4o-2024-11-20"
mini_model = "gpt-4o-mini-2024-07-18"
good_model = "chatgpt-4o-latest"

#api_key = getpass.getpass("Enter your OpenAI API Key: ")
n_prev, topk_episodic = 5, 2
max_steps, n_attempts = 20, 1  # 최대 턴 수를 20으로 설정 (예시)

curr_location = "Inside the Operations Conference Barracks"
observation = "A user and an NPC are talking inside the Operations Conference Barracks. They are sitting at the same table."
valid_actions = [
    "Please choose one of the actions below (do not answer by creating a new action)",
    "(Gesture actions:) Do Bashful, Do Happy, Do Crying, Do Thinking, Do Looking, Do No, Do Fist Pump, Do Agreeing, Do Arguing, Do Thankful, Do Excited, Do Clapping, Do Rejected, Do Look Around",
    "(Move/Item Actions: Go to center, Go to front, Go to left, Go to right, Pick up ball, Dron ball, Do use_ball)"
#    "(Move actions:) Go to sofa, Go to table, Go to pos, Go to door, Go to chair",
#    "(Item actions:) Pick up lance, Drop lance, Use lance, Pick up snack, Drop snack, Use snack"
#    "Sit chair, Turn on pos"
                 ]

log = Logger(log_file)

# -- 에이전트 생성 --
agent = GPTagent(model=default_model, system_prompt=default_system_prompt, api_key=api_key)
agent_plan = GPTagent(model=default_model, system_prompt=system_plan_agent, api_key=api_key)
agent_action = GPTagent(model=good_model, system_prompt=system_action_agent, api_key=api_key)
agent_status = GPTagent(model=default_model, system_prompt=system_status_agent, api_key=api_key)

# 초기 plan_agent 출력 예시 (최초 계획)
initial_plan0 = f'''{{
"plan_steps": [
    {{
      "step_number": 1,
      "sub_goal": "사용자(병사)에게 반말로 인사하며, 간단한 자기소개와 배경을 나눈다.",
      "reason": "서로에 대해 알아가며 신뢰를 형성하고, 편안한 대화 분위기를 만든다.",
      "status": "not completed"
    }},
    {{
      "step_number": 2,
      "sub_goal": "본인(자신)의 간단한 자기소개를 진행한다.",
      "reason": "자신의 역할, 배경 및 특성을 명확히 하여 상대방과의 대화에 도움을 준다.",
      "status": "not completed"
    }}
],
"your_emotion": {{
      "your_current_emotion": "curious and friendly",
      "reason_behind_emotion": "새로운 동료와 처음 만나 서로에 대해 알아가는 과정에 기대와 호기심을 느낀다."
}},
"context_info": "현재 상황: 처음 만난 상태에서 전투 전략보다는 서로의 자기소개에 집중하여 친밀감을 형성하는 것이 중요하다.",
"reactive_plan": "대화가 원활하게 진행되지 않거나 계획에 없는 상황이 발생하면, 당황하거나, 머뭇거리거나 얼버무리며 exception flag를 호출한다."
}}'''

def mark_completed_step(plan_json, step_number):
    # 해당 step_number에 해당하는 항목에 "status": "completed"를 추가
    for step in plan_json.get("plan_steps", []):
        if step.get("step_number") == step_number:
            step["status"] = "completed"
            step["reason"] = step.get("reason", "") + " (completed)"
    return plan_json

# 최근 5턴 동안의 관련 지식을 중복 주제 없이 보관 (OrderedDict 사용)
recent_knowledge = OrderedDict()

# 지식 검색용 Retriever (Retriever 클래스가 정의되어 있다고 가정)
knowledge_retriever = Retriever(device='cpu')

#########################################################
# 1. 상태 관리 agent (get_status) 구현
#    planning 호출 시 누적 history를 기반으로 상태 평가
#########################################################
def get_status(history_context):
    """
    history_context: 누적된 history(문자열)를 받아서 agent_status를 통해
    JSON 형식의 상태 패널을 반환합니다.
    출력 예시:
      {
         "mental_energy": "보통",
         "user_trust": "높음",
         "current_task": "대화"
      }
    """
    prompt = system_status_agent + "\n" + history_context
    status_output, cost_status = agent_status.generate(prompt, jsn=True, t=0.5)
    try:
        status_json = json.loads(status_output)
    except Exception as e:
        status_json = {
            "mental_energy": "보통",
            "user_trust": "높음",
            "current_task": "대화"
        }
    log("Status: "+str(status_json))
    return status_json


#########################################################
# 2. planning 함수 수정: 상태창 정보를 프롬프트에 포함
#    에너지와 신뢰도 보충 설명도 함께 추가
#########################################################
def planning(condition, observations, observation, relevant_episodes, related_topics, previous_plan_json, related_knowledge, status):
    """
    status: get_status()로부터 받은 상태 패널 딕셔너리
    """
    # 에너지와 신뢰도에 따른 보충 설명 생성
    supplementary_energy = energy_prompts.get(status["mental_energy"], "")
    supplementary_trust = trust_prompts.get(status["user_trust"], "")

    status_info = f"""상태창:
    - 정신적 에너지: {status['mental_energy']} ({supplementary_energy})
    - 사용자 신뢰도: {status['user_trust']} ({supplementary_trust})
    - 현재 작업: {status['current_task']}
추가 Planning 지침: {status_prompts[status['current_task']]['planning']}"""

    prompt = f"""
1. State: {status_info}
2. History: {observations}
3. Current observation: {observation}
4. Relevant episodes: {relevant_episodes}
5. Related topics: {related_topics}
6. Previous plan: {previous_plan_json}
7. Predefined Knowledge: {related_knowledge}
"""
    if condition == 'count 5':
        prompt += turn_count_plan_prompt
    elif condition == 'excepsion':
        prompt += exception_plan_prompt
    else:
        prompt += completed_plan_prompt

    plan_output, cost_plan = agent_plan.generate(prompt, jsn=True, t=0.6)
    log("Plan Agent Response: " + plan_output)
    return plan_output

#########################################################
# 3. choose_action 함수 수정: 상태창 정보를 프롬프트에 포함
#    (에너지와 신뢰도 보충 설명 추가)
#########################################################
def choose_action(observations, observation_with_conversation, relevant_episodes, related_topics, current_plan, valid_actions, related_knowledge, status):
    supplementary_energy = energy_prompts.get(status["mental_energy"], "")
    supplementary_trust = trust_prompts.get(status["user_trust"], "")

    status_info = f"""상태창:
    - 정신적 에너지: {status['mental_energy']} ({supplementary_energy})
    - 사용자 신뢰도: {status['user_trust']} ({supplementary_trust})
    - 현재 작업: {status['current_task']}
추가 행동 지침: {status_prompts[status['current_task']]['action']}"""

    prompt = f"""
1. State: {status_info}
2. History: {observations}
3. Latest observation: {observation_with_conversation}
4. Relevant episodes: {relevant_episodes}
5. Related topics: {related_topics}
6. Current plan (Focus on not-completed step!): {current_plan}
7. Predefined Knowledge: {related_knowledge}
8. Possible actions: {valid_actions}
Use context, reactive plan, and any additional information as needed.
Generate a JSON object exactly in the following format:
{{
  "action": "One selected action from the provided list.",
  "npc_response": "A concise dialogue line that is context-aware.",
  "translated_npc_response": "Korean version of npc_response. All letters must be Korean.",
  "facial_expression": "One of neutral, fun, surprised, angry, joy, sorrow.",
  "completed_step": <number>,
  "exception_flag": <boolean>
}}
Do not write anything else.
"""
    t = 1
    action_output, cost_action = agent_action.generate(prompt, jsn=True, t=t)
    try:
        action_json = json.loads(action_output)
        npc_response = action_json["npc_response"]
        translated_npc_response = action_json["translated_npc_response"]
        action = action_json["action"]
        facial_expression = action_json["facial_expression"]
        completed_step = action_json["completed_step"]
        exception_flag = action_json["exception_flag"]
    except Exception as e:
        log("!!!INCORRECT ACTION CHOICE!!!")
        npc_response = "죄송합니다, 이해하지 못했어요."
        facial_expression = "sorrow"
        action = "look"
        completed_step = -1
        exception_flag = False
    return npc_response, translated_npc_response, action, facial_expression, completed_step, exception_flag




# ----------------------------------------------------------------
# 로깅 설정
# ----------------------------------------------------------------
logging.basicConfig(
    level=logging.DEBUG,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[logging.StreamHandler()]
)
logger = logging.getLogger(__name__)

def Logger(log_file):
    def log_func(msg):
        print(msg)
    return log_func
log = Logger(log_file)

# ----------------------------------------------------------------
# Flask App, CORS, Ngrok 준비
# ----------------------------------------------------------------
app = Flask(__name__)
CORS(app)

# API 키 검증 및 토큰 관리 (예시)
API_KEYS = {
    "1": 10000,
    "2": 5000,
}


# ----------------------------------------------------------------
# 클라이언트별 GameSession 관리
# ----------------------------------------------------------------
client_sessions = {}  # key: client_id, value: GameSession instance

# ----------------------------------------------------------------
# 클라이언트 세션 취득 (없으면 새로 생성)
# ----------------------------------------------------------------
def get_or_create_session(client_id, client_api):
    if client_id not in client_sessions:
        logger.debug(f"Creating new session for client {client_id}")
        session = GameSession(client_api)
        client_sessions[client_id] = session
    else:
        session = client_sessions[client_id]
    return session

# ----------------------------------------------------------------
# GameSession 클래스: 한 클라이언트의 전체 게임 상태를 관리
# ----------------------------------------------------------------
class GameSession:
    def __init__(self, client_api):
        # 기존 초기화 코드 그대로 유지
        self.client_api = client_api
        self.api_key = api_key
        self.history = []             # 각 턴의 대화 기록
        self.plan0 = initial_plan0    # 초기 플랜 (JSON 문자열)
        self.current_status = {
            "mental_energy": "보통",
            "user_trust": "보통",
            "current_task": "대화"
        }
        self.subgraph = []            # 직전 메모리 검색 결과
        self.locations = set([curr_location])
        self.prev_npc = ""            # 이전 턴의 NPC 발화
        self.count = 0
        self.recent_knowledge = OrderedDict()
        self.graph = ContrieverGraph(
            default_model,
            system_prompt="You are a helpful assistant",
            api_key=self.api_key,
            device='cpu',
            debug=False
        )

    def process_turn_return_update_params(self, user_input, game_status):
        turn_start = time.time()
        self.count += 1
        log(f"Turn {self.count}")

        log(f"Input: {user_input}")
        log(f"Game Status: {game_status}")
        # 1. input_with_status 구성 (이전 NPC 발화 포함)
        observation_with_conversation = ""
        if self.prev_npc:
            observation_with_conversation += self.prev_npc
        observation_with_conversation += user_input

        # 2. 메모리 retrieval
        retrieved_subgraph, top_episodic = self.graph.memory_retrieve(
            observation_with_conversation, self.plan0, self.subgraph,
            recent_n=5, topk_episodic=topk_episodic
        )
        log("Retrieved associated subgraph: " + str(retrieved_subgraph))
        log("Retrieved top episodic memory: " + str(top_episodic))

        # 3. 사전 정의된 지식과 관련된 지식 갱신
        related_knowledge_items = filter_items_by_similarity(
            predefined_knowledge,
            observation_with_conversation,
            threshold=0.37,
            retriever=knowledge_retriever,
            max_n=3
        )
        for subject, content, score in related_knowledge_items:
            self.recent_knowledge[subject] = content
            self.recent_knowledge.move_to_end(subject)
            log("Related knowledge: " + str(subject) + " " + str(content) + " (score: " + str(score) + ")")
        while len(self.recent_knowledge) > 5:
            self.recent_knowledge.popitem(last=False)
        combined_knowledge_str = "; ".join([f"{subj}: {cont}" for subj, cont in self.recent_knowledge.items()])

        # 4. 행동 선택: choose_action 실행
        npc_response, translated_npc_response, action, facial_expression, completed_step, exception_flag = choose_action(
            self.history,
            observation_with_conversation + game_status,
            top_episodic,
            retrieved_subgraph,
            self.plan0,
            valid_actions,
            related_knowledge=combined_knowledge_str,
            status=self.current_status
        )
        action_selection_time = time.time() - turn_start
        log("NPC: " + npc_response)
        log("Translated Talk: " + translated_npc_response)
        log("Action: " + action)
        log("Facial expression: " + facial_expression)
        log("Completed step: " + str(completed_step))
        log(f"Time for action selection: {action_selection_time:.2f} sec")

        # 5. 업데이트에 사용할 파라미터 구성 (이후 continue_turn_processing에 사용)
        update_params = {
            "observation_with_conversation": observation_with_conversation,
            "npc_response": npc_response,
            "action": action,
            "completed_step": completed_step,
            "exception_flag": exception_flag,
            "top_episodic": top_episodic,
            "retrieved_subgraph": retrieved_subgraph,
            "combined_knowledge_str": combined_knowledge_str
        }

        # 6. 응답 반환에 사용할 결과 구성
        turn_result = {
            "npc_response": npc_response,
            "translated_npc_response": translated_npc_response,
            "action": action,
            "facial_expression": facial_expression,
            "completed_step": completed_step,
            "exception_flag": exception_flag
        }
        return turn_result, update_params

    def continue_turn_processing(self, observation_with_conversation, npc_response, action,
                                 completed_step, exception_flag,
                                 top_episodic, retrieved_subgraph, combined_knowledge_str):
        """
        choose_action 이후의 남은 처리를 진행하는 기존 함수
        """
        # 기록 업데이트
        combined_entry = f"Observation: {observation_with_conversation}\nNPC: {npc_response}\nAction: {action}"
        self.history.append(combined_entry)
        if len(self.history) > n_prev:
            self.history = self.history[-n_prev:]

        if completed_step != -1:
            log(f"Plan step {completed_step} completed. Marking as completed in the plan.")
            plan_current = json.loads(self.plan0)
            plan_current = mark_completed_step(plan_current, completed_step)
            self.plan0 = json.dumps(plan_current)

        current_plan_json = json.loads(self.plan0)
        all_steps_completed = all(step.get("status") == "completed" for step in current_plan_json.get("plan_steps", []))
        if (self.count >= 5) or exception_flag or all_steps_completed:
            history_context = "\n".join(self.history)
            new_status = get_status(history_context)
            log(f"Updated status (from planning): {new_status}")
            if self.count >= 5:
                condition = 'count 5'
            elif exception_flag:
                condition = 'exception'
            elif all_steps_completed:
                condition = 'finish plan'
            log(f"Plan Agent 재실행 (조건: {condition})")
            plan_response = planning(
                condition,
                self.history,
                observation_with_conversation,
                top_episodic,
                retrieved_subgraph,
                self.plan0,
                related_knowledge=combined_knowledge_str,
                status=new_status
            )
            self.plan0 = plan_response
            self.count = 0

        observed_items, _ = agent.item_processing_scores(observation_with_conversation, self.plan0)
        items = {key.lower(): value for key, value in observed_items.items()}
        log("Crucial items: " + str(items))

        self.graph.update_without_retrieve(
            observation_with_conversation, self.plan0, self.subgraph,
            list(self.locations), action, items, log
        )
        # 업데이트 후 최신 subgraph를 재획득하는 예시(필요 시)
        updated_subgraph, _ = self.graph.memory_retrieve(
            observation_with_conversation, self.plan0, [],
            recent_n=5, topk_episodic=topk_episodic
        )
        self.subgraph = updated_subgraph

        self.prev_npc = "NPC Talk: " + npc_response + "\n NPC Action: " + action + "\n"

@app.route('/api/game', methods=['POST'])
def handle_game_state():
    try:
        data = request.json
        logger.debug(f"Received data: {data}")

        # API 키 검증 및 토큰 차감
        client_api = data.get('api_key')
        if not client_api or client_api not in API_KEYS:
            return jsonify({'error': 'Invalid or missing API key.'}), 401
        if API_KEYS[client_api] <= 0:
            return jsonify({'error': 'API key has no remaining tokens.'}), 403
        API_KEYS[client_api] -= 1
        remaining_tokens = API_KEYS[client_api]

        # client_id로 GameSession 가져오기
        client_id = data.get('client_id') or str(uuid.uuid4())
        session = get_or_create_session(client_id, client_api)

        # 요청 파라미터 파싱
        user_input = data.get('userInput', '')
        request_situation = data.get('request_situation', '')
        npc_status = data.get('npc_status', {})
        world_status = data.get('world_status', {})

        input = "User Talk: " + user_input + "\n" + "Request Situation: " + request_situation + "\n"
        game_status = "NPC Status: " + str(npc_status) + "\n" + "World Status: " + str(world_status) + "\n"

        # process_turn_return_update_params()로 즉시 처리 결과와 업데이트 파라미터 획득
        turn_result, update_params = session.process_turn_return_update_params(input, game_status)

        response = jsonify({
            'client_id': client_id,
            'audio_file': generate_tts_audio(turn_result["translated_npc_response"]),
            'Expression': turn_result["facial_expression"],
            'Talk': turn_result["npc_response"],
            'Action': turn_result["action"],
            'remaining_tokens': remaining_tokens
        })

        # 응답 전송 완료 후 업데이트 실행
        def update_callback():
            session.continue_turn_processing(
                update_params["observation_with_conversation"],
                update_params["npc_response"],
                update_params["action"],
                update_params["completed_step"],
                update_params["exception_flag"],
                update_params["top_episodic"],
                update_params["retrieved_subgraph"],
                update_params["combined_knowledge_str"]
            )
        response.call_on_close(update_callback)
        return response

    except Exception as e:
        logger.error(f"An error occurred: {str(e)}")
        logger.error(traceback.format_exc())
        return jsonify({
            'npc_response': "Error",
            'Talk': f"[System: An error occurred: {str(e)}]",
            'action': "Error",
            'facial_expression': "sorrow",
            'remaining_tokens': 0
        }), 500
    

# ----------------------------------------------------------------
# 메인 실행부
# ----------------------------------------------------------------
if __name__ == '__main__':
    try:
        print("Establishing Ngrok tunnel...")
        public_url = ngrok.connect(5003)
        logger.info(f' * ngrok tunnel "{public_url}" -> "http://127.0.0.1:5003"')
        print(f'Ngrok tunnel established: {public_url} -> http://127.0.0.1:5003')
    except Exception as e:
        logger.error(f"Failed to establish Ngrok tunnel: {e}")
        print("Failed to establish Ngrok tunnel.")

    # Flask 실행
    app.run(port=5003)