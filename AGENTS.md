# Fluxo obrigatório deste projeto

Sempre que uma atualização do Otimizador for concluída:

1. Executar a compilação e todos os autotestes.
2. Atualizar versão, notas e documentação quando a mudança afetar o usuário.
3. Substituir `releases/InstalarOtimizadorDeDesempenho.exe` pela compilação final.
4. Atualizar `releases/SHA256SUMS.txt` com o SHA-256 real do instalador.
5. Publicar `releases/update-manifest.public.json` com a mesma versão, URL e SHA-256 do instalador.
6. Remover executáveis e arquivos temporários fora das pastas de entrega.
7. Revisar o diff e garantir que nenhuma credencial ou informação pessoal será publicada.
8. Criar um commit claro e enviar a branch `main` para `ti1gustavoalves-crypto/Otimizar-windows`.
9. Criar e enviar uma tag semântica quando houver uma nova versão de distribuição.
10. Confirmar no GitHub que código, documentação, instalador, manifesto e checksum estão acessíveis.

Uma atualização não deve ser considerada concluída enquanto o envio e a verificação remota não terminarem, salvo se o usuário pedir explicitamente para manter a alteração apenas local.
