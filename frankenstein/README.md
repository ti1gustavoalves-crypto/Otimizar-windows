# Frankenstein TI 0.3.0

Bancada integrada para Windows 10/11 x64. Desempenho, manutenção, catálogo de programas, navegadores, diagnóstico de aplicativos e ferramentas técnicas funcionam dentro da mesma janela e do mesmo executável.

[Baixar instalador](https://github.com/ti1gustavoalves-crypto/Otimizar-windows/releases/download/frankenstein-v0.3.0/InstalarFrankensteinTI.exe)

## Interface

Janela sem a barra padrão do Windows, redimensionamento pelas bordas, área de arraste e controles de minimizar, maximizar e fechar. Ícone original com letra F, costura e raio. Em **Aparência**, escolha Frankenstein, Minecraft, Gamer, Tecnologia ou Claro: cada tema possui desenho próprio, além de cores e tipografia.

## Integração

- O código do otimizador é compilado no Frankenstein; suas áreas de sistema, atualizações do PC e recuperação permanecem disponíveis.
- O código recuperado dos programas fornecidos de instalação e Navegador SOS está em `integrated`, adaptado à navegação e aos dados compartilhados. O catálogo reúne 48 entradas; os programas do instalador menor já estão incluídos nele.
- Diagnóstico de apps, NX, LabX e vínculo corporativo usam rotinas PowerShell sem janela de console, com resultados na interface.
- Uma sessão compartilhada bloqueia o início simultâneo de operações entre áreas e registra os atendimentos. Entrar na área de desempenho inicia consultas de diagnóstico e monitoramento; não aplica otimizações.
- O pacote contém um único executável de aplicação. Não contém nem inicia os antigos EXEs. Os comandos nativos do Windows, os navegadores usados nos testes e os instaladores dos programas escolhidos continuam sendo processos externos necessários ao trabalho.
- A exclusão indiscriminada de inscrições corporativas não foi incorporada. Automação de energia e ícone próprio de bandeja do antigo otimizador ficam desabilitados nesta integração.

## Instalação, atualização e dados

Instale com `InstalarFrankensteinTI.exe`. O botão **Verificar atualização** usa o canal exclusivo do Frankenstein no GitHub e confere versão, HTTPS, tamanho e SHA-256 antes de abrir o instalador. A atualização não é instalada silenciosamente. O instalador não possui assinatura Authenticode.

A instalação usa `%LOCALAPPDATA%\Programs\FrankensteinTI`. Dados ficam em `Dados`: `Execucoes`, `Otimizador`, `Navegadores`, `Instalacoes` e `interface.json`. Dados antigos do otimizador são copiados para a pasta compartilhada no primeiro início, mantendo a origem para recuperação. A instalação conserva uma cópia `previous-*` e reverte a troca se ela falhar. A desinstalação preserva os dados; não desfaz reparos no Windows ou instalações de terceiros.

Use **Administrador** quando uma ação exigir elevação. Ao reabrir, revise novamente o plano de manutenção; ações não são retomadas silenciosamente. Fechar a janela fica bloqueado enquanto uma operação compartilhada está ativa.

## Compilar e validar

Execute no Windows PowerShell 5.1, com .NET Framework 4.8:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-Release.ps1
```

O build prepara cópias de `../src` em diretório temporário de compilação, sem modificar o produto Otimizador independente. Compila esses arquivos com `src` e `integrated`. Não precisa dos executáveis em `Originais` nem do decompilador. Gera o ícone em vários tamanhos e executa testes de scripts, núcleo, distribuição, interface e motor original antes de empacotar.

Os testes verificam consultas reais somente de leitura, simulações de reparo, proteção de caminhos, hashes, rollback de instalação, persistência, contraste, renderização e hospedagem de 11 áreas na mesma janela. Não comprovam todos os reparos em todos os computadores: valide alterações do Windows em máquina de teste antes de uso amplo.

Publique instalador, checksum e manifesto na tag `frankenstein-vX.Y.Z`; atualize `releases/update-manifest.json` somente com o hash real. O canal do Otimizador independente permanece separado.
