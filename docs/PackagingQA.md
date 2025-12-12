---
title: Packaging & QA Checklist
---

## 1. Chuẩn bị build self-contained

```powershell
$solution = "d:\workspace\project\tools\FlashLaunch\FlashLaunch.sln"
$publishStandaloneDir = "d:\workspace\project\tools\FlashLaunch\artifacts\publish-standalone"
$publishPortableDir = "d:\workspace\project\tools\FlashLaunch\artifacts\publish-portable"

# Standalone (self-contained)
dotnet publish "d:\workspace\project\tools\FlashLaunch\FlashLaunch.UI\FlashLaunch.UI.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -o $publishStandaloneDir

# Portable (framework-dependent)
dotnet publish "d:\workspace\project\tools\FlashLaunch\FlashLaunch.UI\FlashLaunch.UI.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -o $publishPortableDir
```

## 2. Đóng gói MSIX (tham khảo doc chính thức)
1. Mở **MSIX Packaging Tool** → *Application package* → chọn thư mục `$publishStandaloneDir`.
2. Khai báo thông tin Publisher, Version, và Capability theo yêu cầu.
3. Ký gói bằng chứng chỉ nội bộ (PowerShell `New-SelfSignedCertificate`, sau đó `signtool sign`).
4. Kiểm thử cài đặt bằng `Add-AppxPackage .\FlashLaunch.msix` trước khi phân phối.

## 3. Đóng gói Squirrel.Windows
```powershell
$setupDir = "d:\workspace\project\tools\FlashLaunch\artifacts\squirrel"
.
\packages\squirrel.windows\tools\Squirrel.exe `
    --releasify FlashLaunch.nuspec `
    --releaseDir $setupDir `
    --loadingGif assets\\splash.gif
```
- File `FlashLaunch.nuspec` nên tham chiếu tới binaries từ `$publishStandaloneDir`.
- Ký file Setup.exe bằng `signtool` sau khi tạo.

## 4. Logging & Perf verification
- Logs mặc định: `%AppData%\FlashLaunch\logs\flashlaunch-perf.log` (Info về query/execute) và `flashlaunch-error.log` (Warning+).
- Đảm bảo quyền ghi tồn tại trước khi đóng gói.

## 5. QA Checklist (<50ms mục tiêu)
| Hạng mục | Mô tả | Kết quả |
| --- | --- | --- |
| Hotkey | Đổi hotkey trong Settings và kích hoạt lại |  |
| Plugin toggles | Bật/tắt từng plugin → kết quả refresh ngay |  |
| Perf query | 10 truy vấn hỗn hợp < 50ms trung bình (đo bằng perf log) |  |
| Execute | Thực thi AppLauncher, Calculator, WebSearch, SystemCommands |  |
| AppLauncher icon | Kiểm tra icon .lnk hiển thị đúng |  |
| Memory footprint | Sau 30 phút idle < 150 MB Working Set |  |
| Install/Uninstall | Test MSIX & Squirrel gói mới |  |

Ghi chú: Gửi kèm log perf/lỗi khi chuyển build cho QA để họ xác nhận số liệu.
