# HƯỚNG DẪN CHỈNH SỬA EMERGENCY ROAD

## Font giao diện tiếng Việt

- Font đang dùng: `Assets/EmergencyRoad/Fonts/Baloo2-Variable.ttf`.
- Font được gán bằng trường `Ui Font` trong `EmergencyRoadCatalog.asset`; có thể kéo một font Unity khác vào đây để thay toàn bộ giao diện.
- Giấy phép OFL đi kèm tại `Assets/EmergencyRoad/Fonts/OFL.txt`.

File này nằm trực tiếp trong `Assets/EmergencyRoad` để có thể chọn và đọc ngay bằng Inspector của Unity.

## Asset trung tâm

Chọn `Assets/EmergencyRoad/Resources/EmergencyRoadCatalog.asset` để thay đổi tài nguyên:

- `Player Vehicles`: xe người chơi và xe trong menu garage. Phần tử đầu tiên là xe miễn phí.
- `Traffic Vehicles`: xe giao thông dùng làm xe cản trên đường và xe chạy ngang ngã tư.
- `Road Prefabs`: phần tử đầu tiên phải là `Road_1`, đường thẳng ba làn.
- `Crossroad Prefabs`: prefab ngã tư trang trí.
- `Crossroad Metrics`: kích thước `Renderer Size/Center` được builder đo tự động cho từng prefab; không nhập kích thước bằng phỏng đoán.
- `Obstacle Prefabs`: rào chắn, cone, hộp và đạo cụ phụ.
- `Decoration Prefabs`: các tòa nhà hai bên đường.
- `Nature Prefabs`: cây xanh.
- `Street Decoration Prefabs`: đèn đường, biển báo, hàng rào và hydrant.
- `Grass Material`, `Soil Material`: vật liệu đất cỏ hai bên đường.
- `VFX Particle Material`: material URP của bụi xe và hiệu ứng nổ motor.
- `Post Process Profile`: Bloom, ACES, chỉnh màu và vignette.

Sau khi đổi danh sách prefab, chạy `Tools > Emergency Road > Build Game`.

Ngã tư hiện dùng bounds thật: prefab mặc định là khoảng `31.519m x 31.519m`, trong khi `Road_1` dài khoảng `7.465m`. Hệ thống tự dành spacer, đặt đường nhánh tại đúng mép bounds và tạo connector/đất cỏ lấp phần chênh lệch còn lại.

## Asset chỉnh gameplay bằng Inspector

Chọn `Assets/EmergencyRoad/Resources/EmergencyRoadGameplaySettings.asset`. Không cần mở code để chỉnh các mục thường dùng:

- tốc độ đầu, tốc độ tối đa và quãng đường tăng tốc;
- khoảng cách chướng ngại đầu/cuối game;
- khoảng cách ngã tư (`Min/Max Straight Chunks Between Intersections`) và vùng trống hai bên ngã tư (`Intersection Clearance Chunks`);
- xác suất chặn hai làn;
- tỷ lệ xe dừng, rào chắn và đạo cụ phụ;
- kích thước hình ảnh và hitbox xe chướng ngại;
- vùng kiểm tra va chạm bên hông;
- tốc độ, độ trễ bám theo và thời gian cảnh báo motor.

`EmergencyRoadCatalog.asset` có ô `Gameplay Settings`; có thể kéo một Gameplay Settings asset khác vào đây để đổi nhanh bộ cấu hình.

## Chỉnh UI trực tiếp trong scene

Mở `Menu.unity` hoặc `Game.unity`, sau đó mở `Preview Root > Main Menu Canvas` hoặc `Game HUD Canvas`. Các phần tử là component Unity UI thật (`Image`, `Button`, `Slider`, `Text`), có thể thay `Source Image`, màu, material, transition, anchor và kích thước trực tiếp trong Inspector.

## Âm thanh

Các ô thay âm thanh nằm trong `EmergencyRoadCatalog.asset`:

- `Music Clip`: nhạc nền.
- `UI Click Clip`: âm thanh nút bấm.
- `Coin Clip`: âm thanh nhặt tiền.
- `Horn Clip`: tiếng còi.
- `Crash Clip`: tiếng va chạm.

Có thể đặt file `.wav` hoặc `.ogg` mới trong `Assets/EmergencyRoad/Audio`, sau đó kéo vào các ô tương ứng. Nếu để trống, game dùng âm thanh tạo sẵn.

## Material và hiệu ứng

- `Assets/EmergencyRoad/Resources/EmergencyRoadPostFX.asset`: hậu kỳ hình ảnh.
- `Assets/EmergencyRoad/Resources/EmergencyRoadVFX.mat`: bụi đường và vụ nổ motor. Luôn dùng shader tương thích URP Particle để tránh màu hồng.
- `Assets/EmergencyRoad/Resources/GrassGround.mat`: màu cỏ.
- `Assets/EmergencyRoad/Resources/SoilGround.mat`: màu đất.

## Scene và vị trí object

- `Assets/Scenes/Menu.unity`: scene garage, settings và controls.
- `Assets/Scenes/Game.unity`: scene đường, thành phố, ánh sáng và hierarchy xem trực tiếp trong Edit Mode.
- `GAME SCENE AUTHORING/Preview Root`: toàn bộ nội dung xem trước trong Editor.
- `Lane Width`: khoảng cách tâm ba làn, được builder hiệu chỉnh từ bounds `Road_1` (`Road Half Width x 0.47`, hiện khoảng `5.185m`). Player, tiền, xe cản và kiểm tra hitbox đều dùng chung giá trị này.
- `Chunk Spacing`: chiều dài được đo tự động từ renderer của `Road_1`.

Không sửa trực tiếp YAML của scene. Chỉnh trong Inspector hoặc sửa builder rồi chạy lại `Tools > Emergency Road > Build Game`.

## Gameplay

Các thông số chính nằm trong `Assets/EmergencyRoad/Scripts/EmergencyRoadGame.cs`:

- `StartSpeed`, `MaxSpeed`: tốc độ đầu và tốc độ tối đa.
- `ShouldSpawnObstacle`: mật độ và khoảng cách cụm chướng ngại.
- `SpawnGameplay`: số làn bị chặn và làn an toàn.
- `SpawnObstacle`: tỷ lệ khoảng 68% xe, 24% rào chắn, 8% đạo cụ khác.
- `FitObstacleToLane`: chuẩn hóa kích thước rào, cone, hộp và thùng rác.
- `SpawnParkedVehicle`: kích thước và hitbox xe đang dừng.
- `MotorRushDirector`: thời gian cảnh báo motor.
- `MotorRushHazard`: tốc độ, độ trễ bám theo, lạng lách và điểm khóa hướng motor.
- `EmergencyVehicleController.Crumple`: hiệu ứng xe người chơi bị méo khi va chạm.

Tùy chọn `VA CHẠM BÊN HÔNG` chỉ chỉnh được trong phần Cài đặt ở Menu. Khi bật, xe không thể chuyển sang làn có chướng ngại vẫn đang thực sự nằm bên hông.

## Cấu trúc thư mục

- `Assets/EmergencyRoad/Scripts`: code runtime.
- `Assets/EmergencyRoad/Editor`: công cụ tạo catalog và scene.
- `Assets/EmergencyRoad/Resources`: catalog, material và post-processing.
- `Assets/EmergencyRoad/Audio`: âm thanh thay thế.
- `Assets/EmergencyRoad/Prefabs`: prefab riêng của game.
- `Assets/EmergencyRoad/Materials`: material bổ sung.
- `Assets/EmergencyRoad/VFX`: prefab particle và texture hiệu ứng.
