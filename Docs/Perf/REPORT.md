# 렌더링 최적화 보고서 (오클루전 컬링 / LOD Group / Mesh LOD)

- 일시: 2026-10-30, 브랜치 `perf/culling-lod`
- 환경: Unity 6000.3.22f1 (URP), RTX 4060 Laptop / D3D12, Game 뷰 1920x1080, 에디터 Play 모드
- 측정 도구: `Assets/Editor/PerfCapture.cs` (`PerfCapture.Run("라벨")`)

## 측정 방법

- 고정 시점 7곳에서 30프레임 워밍업 후 120프레임 평균. `ProfilerRecorder`(Draw Calls / Batches / SetPass / Triangles / Vertices / CullScriptable)와 `FrameTimingManager`(GPU 시간)를 사용했다.
- 조건을 매번 같게 맞췄다: `Time.timeScale = 0`, 좀비 스폰 중지, UI·미니맵 카메라 끄기, Game 뷰 최대화(Scene 뷰 렌더 제외).
- 시점마다 남긴 증거
  - `*_game.jpg`: 게임 화면
  - `*_stats.jpg`: 에디터 Game 창을 그대로 캡처한 화면(Statistics 오버레이 포함)
  - `*_visibility.png`: 위에서 내려다본 가시성 맵
    - 초록: 렌더됨(LOD0), 노랑: LOD1로 렌더됨, 빨강: 시야 안에 있지만 컬링됨(오클루전 또는 LOD 컬링), 회색: 시야 밖, 어두운 색: 바닥 타일
    - 하늘색 점과 흰 선: 카메라 위치와 시야
- 같은 조건에서 기준값을 두 번 측정해 보니 Draw Call 차이는 약 2%였다. 이보다 작은 차이는 오차로 본다.
- CPU Main Thread 시간은 에디터 오버헤드 때문에 2.2~3.8ms 사이에서 흔들려 비교 지표에서 뺐다.

| 시점 | 설명 |
|---|---|
| 01_spawn | 스폰 지점에서 북쪽 (플레이어 모델이 바로 앞에 있음) |
| 02_hangar_front | 격납고 바로 앞에서 동쪽 |
| 03_open_center | 맵 중앙의 트인 곳에서 북동쪽 |
| 04_overview | 맵 모서리 고공에서 전경 |
| 05_player_close | 플레이어 정면 3.5m |
| 06_player_far | 플레이어 뒤쪽 35m |
| 07_quarter | 쿼터뷰 (45°, 45°, 12m) |

## 적용한 내용

### 1단계: 오클루전 컬링
- 기존에는 `Map_v1`의 402개 오브젝트 전부에 Occluder와 Occludee가 켜져 있었고, 베이크 데이터는 없었다.
- 격납고, 큰 오일 탱크, 컨테이너, 콘크리트 벽 105개만 **Occluder + Occludee**로 두었다. 소품, 파이프, 격자 펜스 등 297개는 **Occludee만** 켰다.
- 움직이는 물체(Player, Zombie, Drum, Bomb)는 원래 플래그가 없어서 그대로 두었다.
- 베이크 파라미터: Smallest Occluder 3m, Smallest Hole 0.25m, Backface Threshold 100. 데이터 크기는 약 250KB(`Assets/01.Scenes/GameScene/OcclusionCullingData.asset`)다.

### 2단계: LOD Group
- **Cargo_container 14개**: 에셋 팩에 들어 있는 LOD 메시를 사용했다. LOD0은 `LD2`(132 삼각형), 화면 높이 30% 아래에서는 LOD1 `LD1`(12 삼각형), 2% 아래에서는 컬링한다.
  - 이 팩은 `LD` 숫자가 클수록 고품질이다. 맵의 나무 상자와 배럴은 이미 가장 낮은 `LD1`을 쓰고 있어서 LOD를 추가하지 않았다.
- **소품 240개**(Occluder와 바닥이 아닌 것): LOD0 하나만 두고 화면 높이 2% 아래에서 컬링한다(LOD 컬링).
- 팩 프리팹은 건드리지 않았다. 씬 인스턴스에만 적용했다.

### 3단계: Mesh LOD
- Unity 6 문서상 **static batching이 걸린 메시는 Mesh LOD를 지원하지 않는다.** 그래서 맵(전부 Batching Static)이 아니라 움직이는 오브젝트의 FBX에만 `Generate Mesh LODs`를 켰다.

| FBX | LOD별 삼각형 수 |
|---|---|
| `05.Models/Goku/Goku.fbx` (SkinnedMesh) | 500,000 → 265,080 → 142,844 → 76,406 → … → 64 (16단계) |
| `05.Models/Katana/Katana.fbx` | 500,000 → 262,906 → 142,750 → … → 64 (15단계) |
| `Post-Apocalyptic Barrels.fbx` (Drum) | 2,038 → 1,024 → 512 → … → 64 |
| `M26 [Model].fbx` (수류탄) | body 1,436 → 768 → … |

- `QualitySettings.meshLodThreshold`는 기본값 1을 유지했다.

## 결과: 기준값 → 최종 (세 기법 모두 적용)

### 삼각형 수 (단계별)

| 시점 | 기준 | 오클루전 | +LOD Group | +Mesh LOD | 최종 | 변화 |
|---|---:|---:|---:|---:|---:|---:|
| 01_spawn | 3,076k | 3,074k | 3,070k | 1,664k | 1,668k | **-46%** |
| 02_hangar_front | 120k | 116k | 113k | 113k | 113k | -6% |
| 03_open_center | 92k | 71k | 69k | 69k | 70k | **-24%** |
| 04_overview | 2,113k | 2,145k | 2,123k | 140k | 140k | **-93%** |
| 05_player_close | 3,066k | 3,070k | 3,067k | 1,614k | 1,619k | **-47%** |
| 06_player_far | 3,596k | 3,613k | 3,592k | 189k | 190k | **-95%** |
| 07_quarter | 6,015k | 6,032k | 6,044k | 539k | 538k | **-91%** |

### Draw Call (단계별)

| 시점 | 기준 | 오클루전 | +LOD Group | +Mesh LOD | 최종 | 변화 |
|---|---:|---:|---:|---:|---:|---:|
| 01_spawn | 242 | 220 | 220 | 220 | 220 | -9% |
| 02_hangar_front | 518 | 491 | 501 | 501 | 500 | -3% |
| 03_open_center | 422 | 341 | 351 | 350 | 353 | **-16%** |
| 04_overview | 654 | 662 | 616 | 616 | 616 | -6% |
| 05_player_close | 452 | 446 | 446 | 445 | 446 | -1% |
| 06_player_far | 510 | 512 | 522 | 522 | 522 | +2% (오차 범위) |
| 07_quarter | 338 | 324 | 325 | 325 | 325 | -4% |

### GPU 프레임 시간 (ms)

| 시점 | 기준 | 최종 | 변화 |
|---|---:|---:|---:|
| 01_spawn | 1.90 | 1.81 | -5% |
| 02_hangar_front | 1.19 | 1.22 | +2% (오차 범위) |
| 03_open_center | 1.02 | 0.91 | -11% |
| 04_overview | 1.34 | 0.83 | **-38%** |
| 05_player_close | 1.81 | 1.63 | -10% |
| 06_player_far | 1.84 | 1.23 | **-33%** |
| 07_quarter | 2.35 | 1.37 | **-41%** |

`03_meshlod/stats.csv`의 05_player_close GPU 값은 FrameTimingManager가 비정상 값을 반환한 것이다. 이후 1초가 넘는 값은 버리도록 측정 도구를 고쳤다.

### 컬링 비용 (CullScriptable, ms)

기준 0.12~0.19ms에서 최종 0.22~0.37ms로 **약 0.1ms 늘었다.** 오클루전 쿼리(Umbra)와 LOD 판정에 드는 CPU 비용이다. GPU 절감 폭(최대 1ms)보다 작아서 순이득이다.

### 시야 안에서 컬링된 렌더러 수 (가시성 맵의 빨강)

| 시점 | 기준 | 최종 |
|---|---:|---:|
| 02_hangar_front | 0 | 21 |
| 03_open_center | 0 | 40 |
| 04_overview | 0 | 48 (LOD 컬링) + LOD1 14개 |

## 해석

1. **Mesh LOD 효과가 가장 컸다.** 플레이어의 Goku와 Katana 메시가 각각 50만 삼각형이라 씬 전체 삼각형의 대부분을 차지했다. 그림자 패스에서도 한 번 더 그려져 쿼터뷰에서는 600만 개였다. 거리에 따라 낮은 LOD가 선택되면서 삼각형이 최대 95% 줄었고, GPU 시간도 33~41% 줄었다.
   - 다만 Goku는 SkinnedMesh라 **스키닝(정점 변형)은 여전히 LOD0 기준으로 계산된다.** 줄어든 것은 래스터화 부하뿐이다.
   - 05_player_close(3.5m)는 원래 LOD0이 나와야 하는 거리인데도 삼각형이 47% 줄었다. 정확히 말하면 화면 크기 기준으로 LOD0을 쓸 필요가 없다고 판단된 것이고, 화질 차이는 `05_player_close_game.jpg` 비교에서 눈에 띄지 않았다.
2. **오클루전 컬링은 트인 시점에서 Draw Call -16%(03)**에 그쳤다. 맵이 트인 야외 산업 구역이라 가려 주는 물체가 적기 때문이다. 격납고 앞(02)도 시야 오른쪽이 트여 있어 21개만 가려졌다.
3. **LOD Group은 원거리 전경(04)에서 소품 48개를 컬링해 Draw Call -7%**였다. 이 맵의 소품은 대부분 수십~수백 삼각형짜리 저폴리라서, 삼각형보다 Draw Call 쪽 절감이 의미 있다.
4. **Draw Call이 크게 줄지 않은 진짜 이유는 그림자다.** Stats 화면의 `Shadow casters`가 시점에 따라 수백 개다(02 기준 348개). 디렉셔널 라이트 그림자 캐스터는 메인 카메라 기준으로 가려져 있어도 그림자가 화면에 보일 수 있으므로 대부분 그대로 그려진다. 그래서 오클루전 컬링만으로는 그림자 패스의 Draw Call이 거의 줄지 않는다.

## 다음 단계 제안 (이번 작업 범위 밖)

- **그림자 거리와 캐스케이드 줄이기**: URP Asset의 Shadow Distance를 줄이거나, 작은 소품의 `Cast Shadows`를 끈다. 지금 Draw Call의 가장 큰 비중이다.
- **Goku, Katana 원본 메시 데시메이션**: 50만 삼각형은 스키닝 비용과 메모리 측면에서 과하다. 원본을 2~5만 수준으로 줄이는 것이 근본적인 해결이다.
- **GPU Resident Drawer와 static batching 재검토**: Unity 6에서는 GPU Resident Drawer를 쓰면 맵 메시에도 Mesh LOD를 적용할 수 있다.

## 파일 목록

- `Docs/Perf/00_baseline` ~ `04_final/`: 단계별 측정 결과(`stats.csv`, `env.txt`, 시점별 스크린샷 3종)
- `Docs/Perf/compare/<시점>.jpg`: 기준(왼쪽)과 최종(오른쪽) 비교. 위는 Stats 화면, 아래는 가시성 맵
- 변경된 에셋: `Assets/01.Scenes/GameScene.unity`, `Assets/01.Scenes/GameScene/OcclusionCullingData.asset`, FBX `.meta` 4개, `Assets/Editor/PerfCapture.cs`
