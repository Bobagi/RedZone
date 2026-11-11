# Red Zone

Um jogo sobre semáforos de sistemas operacionais. Feito em 2019 para a disciplina de Sistemas Operacionais por
Gustavo Antonio Perin e Natã Daniel Gomes, e refeito em 2026 (Unity 6).

## Como se joga

- A cada poucos segundos o sistema chama `fork()` e uma **thread** nasce numa **entrada verde**.
- Todas as threads em execução obedecem à **mesma instrução**: as setas (ou WASD, d-pad, arrastar na tela)
  movem todas de uma vez. Threads nascidas na entrada da direita andam espelhadas na horizontal.
- Cada thread roda por um **quantum** (o anel). Quando ele acaba, ela para onde estiver e continua ocupando a casa.
- As áreas coloridas são **semáforos** (o número S é o de vagas livres):
  - **entrada verde**: mutex (S=1). Uma thread parada nela tranca a entrada.
  - **região crítica** (vermelha): S vagas. Uma thread a mais = **condição de corrida**, o kernel cai.
  - Sem entrada livre, o `fork()` empurra a thread para a região crítica com pouco tempo. Sem lugar ali: **deadlock**.
  - **V()** (azul): uma thread em execução que pisa nele libera a vaga presa mais antiga.

Modos: **Campanha** (8 níveis, um conceito por nível, estrelas), **Clássico** (as regras e números do jogo de
2019, 116 threads para vencer) e **Infinito** (recorde). Português, inglês e espanhol.

## Projeto

- Unity **6000.5.1f1**, uGUI + Input System, alvo **WebGL** (itch.io) e Windows.
- Tudo nosso em `Assets/_Project/`: `Scripts/Core` (regras sem Unity: `Board`, `Levels`, `Bot`, `Loc`, `Settings`),
  `Scripts/UI` (toda a interface montada em código), `Scripts/Game` (`GameFlow`, `GameInput`, `Tutorial`,
  `AutoPlayer`), `Scripts/Editor` (construtor da cena e builds), `Tests/EditMode` (regras do tabuleiro).
- **A cena é gerada**: menu `Red Zone > Rebuild Scene`. Não editar `Scenes/RedZone.unity` à mão.
- **Números de jogo num lugar só**: `Scripts/Core/Levels.cs` (mapas, quantum, ritmo do fork, metas).
- Textos: `SourceAssets/Tools/make_loc.py` gera `Settings/Resources/Localization/ui.txt` (chave, pt-BR, en, es).
- Sons e músicas sintetizados: `python -I SourceAssets/Tools/make_audio.py` (efeitos WAV, músicas OGG em loop).
- Builds: `Red Zone > Build WebGL` (saída `Builds/WebGL`) e `Red Zone > Build Windows`.
- Testes: Test Runner (EditMode) ou `Unity -batchmode -runTests -testPlatform EditMode`.
- **Robô de teste**: `bash SourceAssets/Tools/playtest.sh` gera a build de Windows, joga todas as telas e níveis com o
  `Bot` e grava capturas e `autoplay.log` em `autoplay/` (editor fechado).

O projeto original de 2019 (recuperado da build de Windows em `Comp3/`) está no histórico do git, commit `ab08606`.
