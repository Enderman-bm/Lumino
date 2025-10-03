# -*- coding: utf-8 -*-
import os

def replace_in_file(filepath):
    # 只处理文本文件扩展名
    text_extensions = {'.cs', '.axaml', '.md', '.txt', '.json', '.xml', '.sln', '.csproj', '.xaml', '.config', '.manifest', '.log'}
    if not any(filepath.endswith(ext) for ext in text_extensions):
        return
    encodings_to_try = ['utf-8', 'gbk', 'latin1', 'cp1252']
    content = None
    for enc in encodings_to_try:
        try:
            with open(filepath, 'r', encoding=enc) as f:
                content = f.read()
            break
        except UnicodeDecodeError:
            continue
    if content is None:
        print(f'Skipped file with unknown encoding: {filepath}')
        return
    new_content = content.replace('DominoNext', 'Lumino')
    if new_content != content:
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(new_content)
        print(f'Replaced in {filepath}')

def main():
    for root, dirs, files in os.walk('.'):
        dirs[:] = [d for d in dirs if d != '.git']
        for file in files:
            filepath = os.path.join(root, file)
            if os.path.basename(filepath) == 'replace_script.py':
                continue
            replace_in_file(filepath)

if __name__ == '__main__':
    main()