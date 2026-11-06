import json

transcript_path = r"C:\Users\ADMIN\.gemini\antigravity\brain\efe16963-2c83-48d3-8a4e-7dbd6e0d1dc6\.system_generated\logs\transcript_full.jsonl"
output_path = r"scratch\second_master_prompt.txt"

with open(transcript_path, "r", encoding="utf-8") as f:
    lines = f.readlines()

for line in reversed(lines):
    try:
        data = json.loads(line)
        content = data.get("content", "")
        if "pause. what im about to send is the second master prompt" in content or "Second Master Overhaul" in content or "Principal Unity 6 (C#) Systems Architect, UI/UX Technical Artist" in content:
            with open(output_path, "w", encoding="utf-8") as out:
                out.write(content)
            print(f"Found second master prompt, length: {len(content)}")
            break
    except Exception:
        pass
