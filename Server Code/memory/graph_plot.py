
import os
import matplotlib.pyplot as plt
import matplotlib.font_manager as fm
import networkx as nx
from adjustText import adjust_text
import textwrap

# Nanum 폰트가 설치된 디렉토리 내의 .ttf 파일 목록 가져오기
nanum_font_paths = [os.path.join(root, file)
                    for root, dirs, files in os.walk('/usr/share/fonts/truetype/nanum')
                    for file in files if file.endswith('.ttf')]

# 설치된 Nanum 폰트가 있는지 확인하고, 첫 번째 폰트를 사용하도록 설정
if nanum_font_paths:
    font_path = nanum_font_paths[0]
    fm.fontManager.addfont(font_path)
    font_name = fm.FontProperties(fname=font_path).get_name()
    plt.rcParams['font.family'] = font_name
    print(f"Using font: {font_name}")
else:
    print("Nanum 폰트를 찾을 수 없습니다. 다른 한글 폰트를 사용하세요.")

plt.rcParams['axes.unicode_minus'] = False  # 마이너스 기호 깨짐 방지

def plot_contriever_graph(graph):
    """

    """
    # Directed NetworkX 그래프 생성
    G = nx.DiGraph()
    for triplet in graph.triplets:
        subject, obj, attrs = triplet
        relation = attrs.get("label", "")
        G.add_node(subject)
        G.add_node(obj)
        G.add_edge(subject, obj, relation=relation)

    # spring_layout을 사용하여 노드들이 균일하게 분포되도록 설정 (k값과 반복 횟수를 조정)
    pos = nx.spring_layout(G, k=1.5, iterations=100, seed=42)

    # 고정된 큰 사이즈의 Figure 생성
    fig, ax = plt.subplots(figsize=(12, 12))

    # 노드 그리기 (노드 크기 증가)
    nx.draw_networkx_nodes(G, pos, node_size=3000, node_color='lightblue', alpha=0.9, ax=ax)

    # 직선 에지 그리기 (화살표 크기 증가)
    nx.draw_networkx_edges(
        G, pos, arrowstyle='->', arrowsize=25, edge_color='gray', ax=ax
    )

    # 노드 라벨을 ax.text를 통해 그리며, 위치 겹침을 방지하기 위해 텍스트 객체를 모읍니다.
    texts = []
    for node, (x, y) in pos.items():
        wrapped_node = textwrap.fill(node, width=15)
        text = ax.text(x, y, wrapped_node, fontsize=12, fontweight='bold',
                       color='black', ha='center', va='center')
        texts.append(text)

    # adjust_text를 이용해 라벨 간의 겹침을 최소화
    adjust_text(texts, arrowprops=dict(arrowstyle='->', color='gray', lw=0.5))

    # 에지 라벨을 에지 중간에 그리며, 긴 텍스트는 자동 줄바꿈 처리
    edge_labels = nx.get_edge_attributes(G, 'relation')
    for (n1, n2), label in edge_labels.items():
        x1, y1 = pos[n1]
        x2, y2 = pos[n2]
        xm, ym = (x1 + x2) / 2, (y1 + y2) / 2
        wrapped_label = textwrap.fill(label, width=15)
        ax.text(
            xm, ym, wrapped_label, fontsize=10, color='red',
            bbox=dict(facecolor='white', alpha=0.7, boxstyle='round,pad=0.2'),
            horizontalalignment='center', verticalalignment='center'
        )

    ax.set_title("Cognition Graph", fontsize=26)
    ax.axis("off")
    plt.tight_layout()
    plt.show()
