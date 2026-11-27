# Frankenstein TI 0.4.0

Bancada integrada para Windows 10/11 x64. Desempenho, manutenção, catálogo de programas, navegadores, diagnóstico de aplicativos e ferramentas técnicas funcionam dentro da mesma janela e do mesmo executável.

[Baixar instalador](https://github.com/ti1gustavoalves-crypto/Otimizar-windows/releases/download/frankenstein-v0.4.0/InstalarFrankensteinTI.exe)

## Novidades da versão 0.4.0

- **Diagnóstico geral:** primeiro cartão da página inicial. Reúne hardware, espaço livre, saúde informada pelo Windows para HDs/SSDs, contadores de confiabilidade por dispositivo, sensores compatíveis, configuração de rede/proxy e eventos de bugcheck. Dados ausentes aparecem como indisponíveis. Não identifica sozinho a causa de uma tela azul nem garante a saúde futura de um disco. O inventário de rede não equivale a um teste de acesso à internet; o diagnóstico de navegadores complementa a análise.
- **Central de atendimentos:** em Histórico ou clicando no rodapé, acesse diagnóstico, fila, histórico e recuperação. Trabalhos aguardam sua vez. Pendências podem ser canceladas; consultas suportadas usam cancelamento cooperativo; instalações em lote param entre programas. Reparos críticos em andamento não oferecem encerramento forçado.
- **Proteção antes de alterações:** manutenção, reparos, instalações e ações técnicas exigem confirmação de um novo ponto de restauração. Falha, proteção desabilitada ou limite de frequência bloqueiam a operação; o app não muda essa política. O Windows normalmente limita `Checkpoint-Computer` a um ponto por dia. Consulte a [documentação da Microsoft](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/checkpoint-computer?view=powershell-5.1).
- **Rede corporativa:** redefinição do proxy não vem marcada e não é selecionada pelo diagnóstico. Exige revisão adicional. WinHTTP e servidores DNS permanecem preservados. O proxy do usuário possui backup e desfazer específico, restrito ao mesmo usuário/computador e sem sobrescrever alterações posteriores. Limpar cache DNS não tem desfazer.
- **Recuperação:** localiza backups NX, registros de navegadores, checkpoints e proxy; oferece acesso à restauração do otimizador. Indica quando a reversão é direta, feita pela ferramenta específica ou manual. Não promete desfazer limpeza definitiva, reset Winsock ou instaladores de terceiros.
- **PDF local:** exporte diagnóstico, fila, histórico completo ou atendimento selecionado. Relatórios de texto de até 500 KB são anexados ao atendimento quando disponíveis na pasta de dados. Nenhum relatório é enviado para a internet. A fonte PDF cobre português/alfabeto ocidental; símbolos fora dessa codificação podem aparecer como `?`.
- **Atualizações:** até três tentativas em falhas transitórias, cancelamento do download e validação de cada redirecionamento HTTPS. Tamanho, SHA-256 e identidade continuam obrigatórios. Erro de integridade não é repetido como simples falha de rede. O manifesto continua confiando na conta/repositório GitHub; não possui assinatura criptográfica independente.
- **Portátil:** [baixar ZIP](https://github.com/ti1gustavoalves-crypto/Otimizar-windows/releases/download/frankenstein-v0.4.0/FrankensteinTI-0.4.0-Portatil.zip). Extraia em uma pasta gravável e execute `FrankensteinTI.exe`; ainda requer administrador para as funções de suporte. Dados ficam ao lado do EXE. Para atualizar, extraia o novo ZIP em outra pasta e copie `Dados` com o aplicativo fechado. O botão de atualização direciona à versão no GitHub, sem instalar a cópia portátil.

Os testes automatizados e a atualização em pasta isolada não substituem reparos reais em Windows descartável. Veja os [cenários pendentes de VM](VALIDACAO-VM.md). A distribuição continua sem assinatura Authenticode até existir um certificado confiável.

## Aparência

Janela sem a barra padrão do Windows, redimensionamento pelas bordas, área de arraste e controles de minimizar, maximizar e fechar. Ícone original com letra F, costura e raio. Em **Aparência**, escolha Frankenstein, Minecraft, Gamer, Tecnologia ou Claro: cada tema possui desenho próprio, além de cores e tipografia.

## Integração

- O código do otimizador é compilado no Frankenstein; suas áreas de sistema, atualizações do PC e recuperação permanecem disponíveis.
- O código recuperado dos programas fornecidos de instalação e Navegador SOS está em `integrated`, adaptado à navegação e aos dados compartilhados. O catálogo reúne 48 entradas; os programas do instalador menor já estão incluídos nele.
- Diagnóstico de apps, NX, LabX e vínculo corporativo usam rotinas PowerShell sem janela de console, com resultados na interface.
- Uma fila compartilhada serializa operações entre áreas, permite cancelar pendências e registra os atendimentos. Entrar na área de desempenho inicia consultas de diagnóstico e monitoramento; não aplica otimizações.
- O pacote contém um único executável de aplicação. Não contém nem inicia os antigos EXEs. Os comandos nativos do Windows, os navegadores usados nos testes e os instaladores dos programas escolhidos continuam sendo processos externos necessários ao trabalho.
- A exclusão indiscriminada de inscrições corporativas não foi incorporada. Automação de energia e ícone próprio de bandeja do antigo otimizador ficam desabilitados nesta integração.

## Instalação, atualização e dados

Instale com `InstalarFrankensteinTI.exe`. O botão **Verificar atualização** usa o canal exclusivo do Frankenstein no GitHub e confere versão, HTTPS, tamanho e SHA-256 antes de abrir o instalador. A atualização não é instalada silenciosamente. O instalador não possui assinatura Authenticode.

O pipeline está preparado para assinar aplicativo e instalador quando houver um certificado confiável. Consulte [configuração e validação da assinatura](ASSINATURA.md). Sem certificado configurado, os arquivos continuam explicitamente sem assinatura; os instaladores já publicados não são alterados.

A instalação usa `%LOCALAPPDATA%\Programs\FrankensteinTI`. Dados ficam em `Dados`: `Execucoes`, `Otimizador`, `Navegadores`, `Instalacoes` e `interface.json`. Dados antigos do otimizador são copiados para a pasta compartilhada no primeiro início, mantendo a origem para recuperação. A instalação conserva uma cópia `previous-*` e reverte a troca se ela falhar. A desinstalação preserva os dados; não desfaz reparos no Windows ou instalações de terceiros.

O aplicativo e o instalador exigem administrador desde a abertura. O Windows solicita consentimento pelo UAC (ou credenciais administrativas, conforme a conta e a política do computador). Se você cancelar, o programa não abre. Ao reabrir, revise novamente o plano de manutenção; ações não são retomadas silenciosamente. Fechar a janela fica bloqueado enquanto uma operação compartilhada está ativa.

## Compilar e validar

Execute no Windows PowerShell 5.1, com .NET Framework 4.8:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-Release.ps1
```

O build prepara cópias de `../src` em diretório temporário de compilação, sem modificar o produto Otimizador independente. Compila esses arquivos com `src` e `integrated`. Não precisa dos executáveis em `Originais` nem do decompilador. Gera o ícone em vários tamanhos e executa testes de scripts, núcleo, distribuição, interface e motor original antes de empacotar.

Os testes verificam consultas reais somente de leitura, simulações de reparo, proteção de caminhos, hashes, rollback de instalação, persistência, contraste, renderização e hospedagem de 11 áreas na mesma janela. Não comprovam todos os reparos em todos os computadores: valide alterações do Windows em máquina de teste antes de uso amplo.

Publique instalador, checksum e manifesto na tag `frankenstein-vX.Y.Z`; atualize `releases/update-manifest.json` somente com o hash real. O canal do Otimizador independente permanece separado.

Atualização 0.3.2: o instalador fecha automaticamente a cópia instalada antes da troca. Na versão 0.3.1 e seguintes, operações em andamento terminam antes do fechamento, e novos trabalhos ficam bloqueados. Versões anteriores recebem o pedido normal de fechar e podem solicitar que você conclua a operação. Após 60 segundos sem liberação, a atualização é cancelada sem substituir arquivos; conclua o trabalho e tente novamente. O instalador não força o encerramento e não fecha outras cópias ou sessões. O instalador também solicita elevação, para conseguir fechar o app elevado.

Revisão 0.3.2: elevação declarada nos dois executáveis e conferida nos recursos PE após compilar. Botões principais destacados, categorias adaptadas ao tema Claro e cabeçalho de navegadores sem sobreposição em 1120×760. Consultas de inicialização e sistema voltam a carregar ao navegar na interface integrada; nenhum reparo é iniciado por essa navegação. A instalação continua por perfil: use a mesma conta administrativa da instalação existente para preservar destino e dados.

Correção 0.3.3: o download aceita callback de progresso ausente e a interface mostra a porcentagem durante a transferência. Falhas ficam registradas no histórico e em Dados/Falhas. O erro de referência nula da atualização foi reproduzido antes da correção e o mesmo download foi validado depois. Para atualizar uma versão afetada, baixe e execute o instalador pelo GitHub uma vez; o atualizador embutido antigo pode falhar antes de baixar a correção.
