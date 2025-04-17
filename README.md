# GOALLM_v7
('Server Code' and 'Unity Script' folder is just for understand.
You can use ipynb and unitupackage file directly.)

![image](https://github.com/user-attachments/assets/b1d2a60c-e318-43fb-a85c-964d1b1c755a)

GOALLM is LLM-based NPC with GOAP algorithm. (GOAP+LLM)
By GOAP, LLM Agent can conrol NPC by natural language.

(ex: Go to table, Pick up lance, Use lance, Do set_tv_state_on ...)



Also, memory system is important.
It control prompt information of LLM agent.
Memory system from AriGraph.

https://github.com/AIRI-Institute/AriGraph/tree/main
This use Graph RAG and vector RAG to understand complicated game environment.



Architecture Diagram is below.

![GOALLM_v7-Overview](https://github.com/user-attachments/assets/11f2dde3-11fc-492b-8841-7b10e9c559d7)

![GOALLM_v7-Server](https://github.com/user-attachments/assets/a26e5a07-3ade-4b5f-8763-b3c30c428e11)

![GOALLM_v7-Client](https://github.com/user-attachments/assets/e319e167-77fd-4048-ad4c-68178e21391a)
