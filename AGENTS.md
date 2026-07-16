# Fluxo obrigatório deste projeto

Sempre que uma atualização do Otimizador for concluída:

1. Executar a compilação e todos os autotestes.
2. Atualizar versão, notas e documentação quando a mudança afetar o usuário.
3. Substituir `releases/InstalarOtimizadorDeDesempenho.exe` pela compilação final.
4. Atualizar `releases/SHA256SUMS.txt` com o SHA-256 real do instalador.
5. Remover executáveis e arquivos temporários fora das pastas de entrega.
6. Revisar o diff e garantir que nenhuma credencial ou informação pessoal será publicada.
7. Criar um commit claro e enviar a branch `main` para `ti1gustavoalves-crypto/Otimizar-windows`.
8. Criar e enviar uma tag semântica quando houver uma nova versão de distribuição.
9. Confirmar no GitHub que código, documentação, instalador e checksum estão acessíveis.

Uma atualização não deve ser considerada concluída enquanto o envio e a verificação remota não terminarem, salvo se o usuário pedir explicitamente para manter a alteração apenas local.

