# Red Zone · guia do projeto

Jogo educativo sobre semáforos (2019, faculdade), refeito em 11/2026 em Unity 6000.5.1f1, uGUI + Input System, alvo WebGL
(itch: bobagi.itch.io/red-zone) e Windows. Leia o `README.md` para as regras do jogo. Sem travessão (em/en dash) em nada.

## Como mexer
- **Regras** só em `Assets/_Project/Scripts/Core/Board.cs` (sem objetos da Unity, testado em `Tests/EditMode/BoardTests.cs`).
  Mudou regra: rode os testes (`Unity -batchmode -runTests -testPlatform EditMode -projectPath .`).
- **Números de jogo** (mapas, capacidade, quantum, ritmo do fork, metas) só em `Core/Levels.cs`. Mapa: `.` livre, `G` entrada,
  `R` região crítica, `S` V(), `#` parede; entradas na metade direita criam threads espelhadas.
- **UI toda em código** (padrão do Wilkins Pest Control): `UI/GameUI.cs` (telas), `UI/BoardView.cs` + `ThreadView.cs` (tabuleiro),
  `UI/Ui.cs` (botão, opção, keycap), `UI/Theme.cs` (cores, fontes, sprites gerados). Trocar idioma reconstrói a UI.
- **Cena gerada**: `Red Zone > Rebuild Scene` (`Editor/RedZoneTools.cs`). A cena só tem câmera, EventSystem e `[RedZone]`.
- **Textos**: `SourceAssets/Tools/make_loc.py` -> `Settings/Resources/Localization/ui.txt` (`Loc.T`, modelo do Beneath the Shroud).
- **Áudio**: `SourceAssets/Tools/make_audio.py` (numpy). OGG longo precisa ser gravado em blocos (libsndfile #1093 estoura a pilha).
  `Audio/Sfx.cs` toca por nome; músicas `menu` e `game` com crossfade.
- **Robô**: `bash SourceAssets/Tools/playtest.sh` (editor fechado). `Game/AutoPlayer.cs` liga com `-autoplay`, joga tudo com
  `Core/Bot.cs` e grava capturas em `autoplay/`. Rodar depois de mexer em níveis, para ver se continuam vencíveis.
- Progresso e opções em PlayerPrefs (`Core/Settings.cs`, prefixo `rz_`).
