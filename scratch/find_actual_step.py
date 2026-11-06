import json

transcript_path = r"C:\Users\ADMIN\.gemini\antigravity\brain\efe16963-2c83-48d3-8a4e-7dbd6e0d1dc6\.system_generated\logs\transcript_full.jsonl"
output_path = r"scratch\actual_prompt_step.txt"

with open(transcript_path, "r", encoding="utf-8") as f:
    for idx, line in enumerate(f):
        try:
            data = json.loads(line)
            content = data.get("content", "")
            if "here. /unity-cli" in content:
                with open(output_path, "w", encoding="utf-8") as out:
                    out.write(content)
                print(f"Found prompt at line {idx}, length: {len(content)}")
                break
        except Exception:
            pass
