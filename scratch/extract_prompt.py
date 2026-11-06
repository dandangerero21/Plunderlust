import json

transcript_path = r"C:\Users\ADMIN\.gemini\antigravity\brain\efe16963-2c83-48d3-8a4e-7dbd6e0d1dc6\.system_generated\logs\transcript_full.jsonl"
output_path = r"scratch\last_user_prompt.txt"

with open(transcript_path, "r", encoding="utf-8") as f:
    for line in f:
        try:
            data = json.loads(line)
            content = data.get("content", "")
            if "ROLE & OBJECTIVE" in content:
                with open(output_path, "w", encoding="utf-8") as out:
                    out.write(content)
                print(f"Found and wrote prompt to {output_path}")
                break
        except Exception as e:
            pass
