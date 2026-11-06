import os
import re

csproj_path = "Assembly-CSharp.csproj"
scripts_dir = os.path.join("Assets", "Scripts")

with open(csproj_path, "r", encoding="utf-8") as f:
    content = f.read()

# Find all existing Compile Include items
existing = set(re.findall(r'<Compile Include="([^"]+)"', content))

added = 0
for root, dirs, files in os.walk(scripts_dir):
    # Skip Editor folder as it belongs to Assembly-CSharp-Editor.csproj
    if "Editor" in dirs:
        dirs.remove("Editor")
    for file in files:
        if file.endswith(".cs"):
            rel_path = os.path.relpath(os.path.join(root, file)).replace("/", "\\")
            if rel_path not in existing:
                item = f'    <Compile Include="{rel_path}" />\n'
                # Insert before </ItemGroup>
                idx = content.find("</ItemGroup>")
                if idx != -1:
                    content = content[:idx] + item + content[idx:]
                    existing.add(rel_path)
                    added += 1
                    print(f"Added {rel_path}")

if added > 0:
    with open(csproj_path, "w", encoding="utf-8") as f:
        f.write(content)
    print(f"Updated {csproj_path} with {added} new files.")
else:
    print(f"{csproj_path} is already completely up to date.")
