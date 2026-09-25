import json
import re
import sys
from pathlib import Path
from docx import Document

design_path = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("design/STS2_挑战点系统_设计文档_v0.2.docx")
doc = Document(design_path)
names = {
    "G-16": "精英奖励封锁", "G-17": "敌人再生", "G-18": "敌人力量", "G-19": "少一张奖励牌",
    "G-20": "开局失去力量", "G-21": "开局失去敏捷", "G-22": "敌人壁垒", "G-23": "相邻火堆限制",
    "G-24": "药水损失", "G-25": "首回合失血",
}
ready = {
    # Only entries with an implemented runtime hook may be selected.
    *(f"G-{n:02d}" for n in range(1, 26)),
    "IC-01", "IC-02", "IC-03", "IC-04", "IC-05", "IC-06", "IC-07", "IC-08",
    "RG-01", "RG-02", "RG-03", "RG-04", "RG-05", "RG-06", "RG-07", "RG-08",
    "NB-01", "NB-02", "NB-03", "NB-04", "NB-05", "NB-06", "NB-07", "NB-08",
    "SL-01", "SL-02", "SL-03", "SL-04", "SL-05", "SL-06", "SL-07", "SL-08",
    "DF-01", "DF-02", "DF-03", "DF-04", "DF-05", "DF-06", "DF-07", "DF-08",
}
rows = []
clarified = {
    "IC-03": ("消耗反噬", "每回合每消耗 2 张牌，向手牌加入 1 张伤口。"),
    "IC-04": ("血肉代价", "因自己打出的牌失去生命时，额外失去 1 点生命。"),
    "IC-05": ("易伤加深", "获得易伤时，额外获得 1 层易伤。"),
    "IC-08": ("虚弱免疫", "敌人无法获得虚弱。"),
    "RG-02": ("负星惩罚", "星数为负时，每欠 2 星失去 1 点力量和敏捷。"),
    "RG-05": ("无色生成上限", "每回合最多生成 5 张无色牌，超出的牌立即移除。"),
    "NB-04": ("灾厄逆流", "灾厄大于当前生命的敌人对玩家造成的伤害提高 50%。"),
    "SL-03": ("敌人坚韧", "每名敌人出现时获得 2 层坚韧；每次被攻击命中后获得相应格挡，层数加 1。"),
    "SL-04": ("奇巧耗能", "每回合每打出 2 张奇巧牌，失去 1 点能量。"),
    "SL-05": ("连打疲劳", "每回合打出 12 张牌后，再打出每张牌失去 1 点能量。"),
    "SL-06": ("毒素衰减", "敌人回合开始时，中毒额外减少 1 层。"),
    "SL-08": ("虚弱反噬", "使敌人获得虚弱时，其获得 1 点临时力量。"),
    "DF-02": ("基础球上限", "闪电和冰霜充能球的数值最多为 33。"),
    "DF-04": ("状态牌回流", "每回合每有 2 张状态牌进入弃牌堆，将第 2 张改放到抽牌堆顶部。"),
    "DF-06": ("高级球限制", "黑暗球数值上限为 33；等离子球数值减少 1，最低为 0。"),
    "DF-08": ("能力牌上限", "每回合至多打出 3 张能力牌。"),
}
for table_index, role in [(5, "common"), (6, "common"), (7, "ironclad"), (10, "regent"), (13, "necrobinder"), (16, "silent"), (19, "defect")]:
    for row in doc.tables[table_index].rows[1:]:
        cells = [c.text.strip() for c in row.cells]
        ident = cells[0]
        if table_index == 5:
            name, desc, cp_text = cells[1], cells[2], cells[3]
        else:
            desc, cp_text = cells[1], cells[2]
            name = names.get(ident, desc.split("，")[0].split("；")[0][:18])
        m = re.search(r"(\d+)\s*[×x]\s*(\d+)", cp_text)
        if m:
            cost, max_rank = map(int, m.groups())
        else:
            cost, max_rank = int(re.search(r"\d+", cp_text).group()), 1
        if ident in clarified:
            name, desc = clarified[ident]
        rows.append({"id": ident, "role": role, "name": name, "description": desc,
                     "cpPerRank": cost, "maxRank": max_rank, "selectable": ident in ready})
out = Path("ChallengePointsData.json")
out.write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"{out}: {len(rows)} entries, {sum(x['selectable'] for x in rows)} selectable")
