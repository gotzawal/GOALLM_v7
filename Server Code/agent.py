# https://github.com/AIRI-Institute/AriGraph/tree/main


import ast
import requests
from time import sleep
from openai import OpenAI


class GPTagent:
    def __init__(self, model, system_prompt, api_key):
        self.system_prompt = system_prompt
        self.model = model
        self.total_amount = 0
        self.client = OpenAI(
            api_key=api_key,
        )

    def generate(self, prompt, jsn = False, t = 0.7):
        if jsn:
            chat_completion = self.client.chat.completions.create(
                messages=[
                    {
                        "role": "system",
                        "content": self.system_prompt,
                    },
                    {
                        "role": "user",
                        "content": prompt,
                    }
                ],
                model=self.model,
                response_format={"type": "json_object"},
                temperature=t
            )
        else:
            chat_completion = self.client.chat.completions.create(
                messages=[
                    {
                        "role": "system",
                        "content": self.system_prompt,
                    },
                    {
                        "role": "user",
                        "content": prompt,
                    }
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

    def item_processing_scores(self, observation, plan):
        prompt = (
            "####\n"
            "You are a retriever part of the agent system that navigates the environment in a text-based game.\n"
            "You will be provided with the agent's observation, what it carries, and the plan it follows.\n"
            "Your task is to extract entities from this data that can later be used to query the agent's memory module to find relevant information to solve the task.\n"
            "Assign a relevance score from 1 to 2 to every entity, reflecting the importance of the entity and potential memories connected to it for the current plan and goals of the agent.\n"
            "Do not extract multiple items in one sentense like 'player and npc'.\n"
            "Pay attention to the main goal of the plan.\n"
            "**IMPORTANT** Ensure that all extracted entities are in English.(Even it's name or written in other language)\n\n"
            "Current observation: {}\n".format(observation) +
            "Current plan: {}\n\n".format(plan) +
            "Answer in the following format:\n"
            '{"entity_1": score1, "entity_2": score2, ...}\n'
            "Do not write anything else\n"
        )
        response, cost = self.generate(prompt, t=0.1)
        entities_dict = ast.literal_eval(response)
        return entities_dict, cost
