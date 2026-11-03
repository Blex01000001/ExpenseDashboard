#!/usr/bin/env python3
"""產生虛構的「天天記帳」匯出 CSV（新版格式，含標籤與備註），供截圖與手動驗證用。

固定亂數種子，同一個 --end 會得到一模一樣的檔案。

用法：python3 scripts/demo-data.py OUTPUT.csv [--end YYYY-MM-DD]
  --end 預設為今天往前 3 天；資料從 --end 的前一年 1 月 1 日開始。
"""
import argparse
import datetime
import random
import uuid

HEADER = "日期,類別,大類別,金額,幣別,成員,帳戶,標籤,備註,收支區分,上次更新,UUID"

# (類別, 大類別, 平均金額, 每 30 天大約幾筆, 標籤, 可能的備註)
CATEGORIES = [
    ("早餐", "Diet", 60, 25, "", ""),
    ("午餐", "Diet", 120, 20, "", "便當, 加蛋"),
    ("晚餐", "Diet", 200, 25, "", "跟朋友聚餐"),
    ("Beverage", "Diet", 50, 15, "", ""),
    ("捷運", "交通", 30, 20, "通勤", ""),
    ("加油", "交通", 900, 2, "通勤,汽車", ""),
    ("房租", "Home", 15000, 1, "", ""),
    ("電費", "Home", 1200, 1, "", ""),
    ("C#", "お勉強", 1500, 1, "學習", ""),
    ("書籍", "お勉強", 500, 2, "學習", ""),
    ("運動", "", 300, 4, "", ""),            # 沒有大類別
    ("ゲーム", "", 300, 2, "", "BA 課金"),     # 日文類別、備註關鍵字
    ("電影", "娛樂", 350, 2, "朋友", "威秀 影城"),
    ("衣服", "購物", 1500, 1, "", "Uniqlo 特價"),
    ("訂閱", "", 650, 1, "", "ChatGPT Plus"),
    ("看醫生", "醫療", 300, 1, "", ""),
]


def csv_field(value: str) -> str:
    return f'"{value}"' if any(c in value for c in ',"\n') else value


def generate(end: datetime.date) -> list[str]:
    rng = random.Random(20250101)
    rows = []
    day = datetime.date(end.year - 1, 1, 1)
    while day <= end:
        for category, major, amount, per_month, tags, note in CATEGORIES:
            if rng.random() < per_month / 30:
                value = round(amount * rng.uniform(0.6, 1.4))
                use_note = note if note and rng.random() < 0.5 else ""
                stamp = f"{day:%Y-%m-%d} {rng.randint(8, 22):02d}:{rng.randint(0, 59):02d}:00"
                guid = str(uuid.UUID(int=rng.getrandbits(128), version=4)).upper()
                rows.append((day, ",".join([
                    f"{day:%Y%m%d}", category, f"{major} " if major else "", str(value), "TWD", "自己", "現金",
                    csv_field(tags), csv_field(use_note), "支", stamp, guid,
                ])))
        day += datetime.timedelta(days=1)
    # 匯出檔是新到舊
    return [line for _, line in sorted(rows, key=lambda r: r[0], reverse=True)]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("output")
    parser.add_argument("--end", type=datetime.date.fromisoformat,
                        default=datetime.date.today() - datetime.timedelta(days=3))
    args = parser.parse_args()
    lines = generate(args.end)
    with open(args.output, "w", encoding="utf-8-sig", newline="\n") as f:
        f.write("\n".join([HEADER, *lines]) + "\n")
    print(f"{args.output}: {len(lines)} rows, {datetime.date(args.end.year - 1, 1, 1)} – {args.end}")


if __name__ == "__main__":
    main()
