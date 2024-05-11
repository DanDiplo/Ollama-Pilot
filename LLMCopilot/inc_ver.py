import sys
import xml.etree.ElementTree as ET

def increment_version(xml_file_path):
    try:
        # 注册命名空间，确保输出时不会添加不必要的 ns0 命名空间前缀
        ET.register_namespace('', 'http://schemas.microsoft.com/developer/vsx-schema/2011')
        
        tree = ET.parse(xml_file_path)
        root = tree.getroot()
        # 注意：使用无前缀的命名空间路径
        identity = root.find(".//{http://schemas.microsoft.com/developer/vsx-schema/2011}Metadata/{http://schemas.microsoft.com/developer/vsx-schema/2011}Identity")
        if identity is None:
            raise ValueError("Identity element not found.")
        
        current_version = identity.get('Version')
        version_parts = current_version.split('.')
        version_parts[-1] = str(int(version_parts[-1]) + 1)  # 增加版本号最后一部分
        new_version = '.'.join(version_parts)
        identity.set('Version', new_version)
        
        tree.write(xml_file_path, encoding='utf-8', xml_declaration=True)
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    project_dir = sys.argv[1]  # 接收命令行传入的项目目录
    # 确保路径拼接正确
    xml_file_path = f"{project_dir}\\source.extension.vsixmanifest"  # Windows 路径使用反斜杠
    increment_version(xml_file_path)
