# Segurança e Pareamento de Dispositivos

O modelo de segurança do **MediaViewer** foi concebido para simplificar a experiência em TVs e dispositivos móveis infantis sem comprometer o controle de acesso à API.

---

## Fluxo de Pareamento de Dispositivos

O pareamento substitui formulários tradicionais de login com usuário e senha no dispositivo cliente por um mecanismo baseado em código temporário:

```text
[Administrador]                [Servidor MediaViewer]              [Dispositivo / App]
       |                                 |                                  |
       | 1. Solicita código de           |                                  |
       |    pareamento no painel web     |                                  |
       | ------------------------------> |                                  |
       |                                 |                                  |
       | 2. Retorna código de 6 dígitos  |                                  |
       |    (válido por 10 minutos)      |                                  |
       | <------------------------------ |                                  |
       |                                 |                                  |
       | 3. Administrador digita o código no aplicativo                     |
       | =================================================================> |
       |                                 |                                  |
       |                                 | 4. POST /api/devices/pair        |
       |                                 |    { code: "123456", name: "..."}|
       |                                 | <------------------------------- |
       |                                 |                                  |
       |                                 | 5. Gera Device Token opaco       |
       |                                 |    Salva hash SHA-256 no banco   |
       |                                 |    Invalida código usado         |
       |                                 |                                  |
       |                                 | 6. Retorna Bearer Token          |
       |                                 | -------------------------------> |
       |                                 |                                  |
       |                                 | 7. Armazena token localmente     |
       |                                 |    e utiliza em chamadas à API   |
```

---

## Armazenamento e Validação de Tokens

1. **Tokens Opacos Aleatórios**: O token gerado é uma sequência criptográfica pseudo-aleatória em Base64.
2. **Armazenamento Seguro no Servidor**: O servidor **não** armazena o token em texto puro; apenas seu hash criptográfico (`SHA-256`) é persistido na tabela `DeviceTokens`.
3. **Revogação Instantânea**: No painel administrativo em `/admin/devices`, o administrador pode visualizar todos os dispositivos ativos e revogar qualquer um com um único clique.
4. **Armazenamento no Cliente**: O aplicativo móvel persiste o token nas preferências protegidas do sistema operacional Android (`Microsoft.Maui.Storage.Preferences`).

---

## Acesso Administrativo

- O painel web administrativo (`/admin`) utiliza autenticação por cookies protegidos com expiração controlada.
- Credenciais administrativas iniciais são fornecidas exclusivamente via variáveis de ambiente (`ADMIN_USERNAME` e `ADMIN_PASSWORD`) no momento da criação do banco.
