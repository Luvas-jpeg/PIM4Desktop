"""Package the Windows CI output for GitHub Releases."""
import hashlib
import json
import os
from pathlib import Path
import shutil
from zipfile import ZIP_DEFLATED, ZipFile

package = Path("package")
downloads = Path("downloads")
downloads.mkdir(exist_ok=True)
api = "https://pim4-api-demo.onrender.com/"
configuration = json.loads((package / "appsettings.json").read_text())
assert configuration["Api"]["BaseUrl"] == api, "A API do pacote não corresponde ao Render."
executable = package / "PIM4AdminDesktop.exe"
assert executable.is_file(), "Executável Windows não encontrado."
info = {"commit": os.environ.get("GITHUB_SHA"), "api": api, "runtime": "win-x64", "selfContained": True}
(package / "build-info.json").write_text(json.dumps(info, indent=2) + "\n")
shutil.copy2(executable, downloads / "MediShop-Admin-Windows-x64.exe")
with ZipFile(downloads / "MediShop-Admin-Windows-x64.zip", "w", ZIP_DEFLATED) as archive:
    for file in sorted(package.rglob("*")):
        if file.is_file() and file.suffix.lower() != ".pdb":
            archive.write(file, file.relative_to(package))
checksums = [f"{hashlib.sha256(file.read_bytes()).hexdigest()}  {file.name}" for file in sorted(downloads.iterdir()) if file.is_file()]
(downloads / "SHA256SUMS.txt").write_text("\n".join(checksums) + "\n")
print("Pacotes Windows preparados com API do Render e checksums.")
