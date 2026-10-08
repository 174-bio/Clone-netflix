# 🎬 CINESTREAM — Plataforma de Streaming de Alta Performance

**CineStream** é uma plataforma cinematográfica de streaming completa, moderna e responsiva desenvolvida com identidade visual própria (design dark premium com destaques em Carmim Neon e Obsidian), catálogo com 32 produções originais (22 filmes e 10 séries completas com múltiplos episódios), player de vídeo HTML5 imersivo com salvamento contínuo de progresso, sistema real de autenticação e gerenciamento de perfil, avaliações interativas por estrelas, pesquisa em tempo real e filtros avançados.

---

## 🌟 Funcionalidades Principais

### 1. Experiência Cinematográfica & Identidade Própria
- **Design Obsidian & Neon Crimson**: Interface imersiva, minimalista e cinematográfica, sem utilizar elementos protegidos ou cópia literal da Netflix.
- **Hero Billboard Dinâmico**: Destaque de alta resolução com metadados 4K Ultra HD, classificação indicativa oficial, sinopse, pontuação da comunidade e botões de ação rápida.
- **Carrosséis Horizontais com Navegação Suave**: Seções como *Continuar Assistindo*, *Minha Lista*, *Em Alta*, *Lançamentos Recentes*, *Séries Que Você Precisa Maratonar*, *Ficção Científica & Cyberpunk*, *Ação & Adrenalina*, *Suspense* e *Documentários*.

### 2. Player de Vídeo Completo & Profissional
- **Reprodução de Vídeos Reais**: Suporte nativo a streams MP4 em alta definição com codecs modernos.
- **Salvamento Automático de Progresso**: Salva a cada 3 segundos e no momento da saída a posição exata da reprodução, alimentando o recurso **Continuar Assistindo**.
- **Controles Avançados**:
  - Play/Pause com atalhos de teclado (`Espaço` ou `K`).
  - Timeline com barra de progresso, scrubber e tooltip com indicador de minutos/segundos.
  - Pular 10 segundos para frente e para trás (`J`, `L` ou setas).
  - Controle de Volume com slider e Mudo (`M`).
  - Velocidade ajustável (0.75x, 1.0x Normal, 1.25x, 1.5x, 2.0x).
  - Seletor de Legendas (Desativadas, Português CC, English CC, Español).
  - Seletor de Qualidade (Auto 4K, 1080p Full HD, 720p HD, 480p).
  - Modo Tela Cheia (`F`).
  - Ocultamento automático de controles após inatividade do mouse.

### 3. Autenticação & Gerenciamento de Perfil
- **Cadastro Completo**: Nome, e-mail, senha criptografada (hash SHA-256 + salt exclusivo) e seleção de avatar estilizado.
- **Login Real & Sessão Persistente**: Armazenamento seguro de token com sincronização contínua com a API.
- **Usuário Demonstração (1 Clique)**: Botão no modal de login para entrada imediata com a conta pré-configurada (`demo@cinestream.tv`).
- **Gerenciamento de Perfil**:
  - Alteração de nome e troca de avatar a partir de galeria de 12 opções exclusivas.
  - Alteração segura de senha.
  - Histórico de títulos assistidos com barra de porcentagem concluída e retomada direta.
  - Preferências de reprodução (Auto-play do próximo episódio, qualidade padrão e idiomas de áudio/legenda).

### 4. Catálogo & Detalhes
- **22 Filmes Originais** de múltiplos gêneros (Ação, Ficção Científica, Suspense, Fantasia, Romance, Documentário, Comédia).
- **10 Séries Originais** completas com temporadas e episódios individuais (título, descrição, duração, thumbnail e vídeo específico).
- **Avaliações com Estrelas**: Sistema interativo de 1 a 5 estrelas salvo por usuário na base de dados.
- **Minha Lista Dinâmica**: Adição e remoção com feedback instantâneo e aba dedicada.

### 5. Busca & Filtros Avançados
- **Pesquisa em Tempo Real**: Busca instantânea por título, gênero, elenco, diretor e sinopse com tratamento de estado vazio.
- **Filtros Combinados**: Filtragem por Tipo (Filmes/Séries), Gênero, Classificação Etária (Livre a 18+), Nota Mínima e Ordenação (Mais Populares, Lançamentos, Melhores Avaliados).

### 6. Responsividade Total
- **Mobile First & Desktop**: Menu inferior flutuante (*Bottom Navigation*) para celulares e tablets, carrosséis com rolagem por toque (*momentum scrolling*), e menus expansíveis para desktop.

---

## 🛠️ Tecnologias Utilizadas

- **Backend**: C# 10 / ASP.NET Core Minimal APIs (.NET 10)
- **Frontend**: HTML5 Semântico, Vanilla JavaScript (ES6+ modular)
- **Estilização**: Vanilla CSS3 com Variáveis de Design, Flexbox, CSS Grid, Glassmorphism e Keyframe Animations
- **Banco de Dados**: SQLite para desenvolvimento local e PostgreSQL para produção no Render; os dados do catálogo, usuários e progresso são persistidos em tabelas com JSONB/JSON e relacionamentos explícitos
- **Vídeos**: Streams em MP4 e uploads gerenciados com armazenamento persistente em disco do Render/volume

---

## 🚀 Como Executar Localmente

### Pré-requisitos
- .NET SDK 8.0, 9.0 ou 10.0 instalado ([Download .NET](https://dotnet.microsoft.com/download))

### Passo a Passo

1. **Abra o terminal no diretório do projeto:**
   ```bash
   cd /workspaces/Clone-netflix
   ```

2. **Configure as variáveis locais (opcional):**
   ```bash
   cp .env.example .env
   ```

3. **Compile a aplicação:**
   ```bash
   dotnet build netflix-clone/NetflixClone.csproj
   ```

4. **Execute o servidor:**
   ```bash
   cd netflix-clone
   dotnet run
   ```

5. **Acesse no seu navegador:**
   Abra [http://localhost:5167](http://localhost:5167)

Por padrão, o app usa SQLite local (`Database__Provider=sqlite` / `Database__Path=Data/cinestream.db`) e uploads locais para desenvolvimento. Em produção, use PostgreSQL e armazenamento de objetos compatível com S3.

### Deploy gratuito no Render com dados fora do filesystem efêmero

O serviço web gratuito do Render pode dormir quando fica sem tráfego e seu filesystem é efêmero. Por isso, o banco e os vídeos não devem ser gravados no disco do serviço. O Blueprint (`render.yaml`) usa o Render apenas para executar o app; o banco e os vídeos são configurados em serviços externos:

- **Neon PostgreSQL Free**: banco externo; o schema JSONB usado pelo app é criado automaticamente no primeiro start.
- **Cloudflare R2 Standard**: armazenamento de vídeos compatível com S3; o serviço aceita MP4/WebM e grava as URLs públicas no catálogo.
- **Render Free Web Service**: hospedagem do app e deploy automático a partir do GitHub.

Os planos gratuitos têm limites e podem mudar. No momento em que esta configuração foi preparada, o Neon Free oferece 1 GB por projeto e o R2 inclui 10 GB-mês, 1 milhão de operações Classe A e 10 milhões Classe B por mês. O R2 pode cobrar uso acima da franquia; confira os limites e configure alertas/budgets na conta antes de ativar cobrança. O serviço gratuito do Render pode levar cerca de um minuto para acordar após ficar inativo.

#### Configurar o PostgreSQL no Neon

1. Crie uma conta e um projeto no Neon usando o plano Free.
2. Copie a connection string PostgreSQL do projeto. Não a coloque no GitHub.
3. No Render, defina `Database__Provider=postgres` e `DATABASE_URL` com essa connection string.
4. O app converte URLs `postgres://`/`postgresql://`, abre conexões pelo pool padrão do Npgsql e cria as tabelas no startup.

#### Configurar vídeos no Cloudflare R2

1. Crie um bucket R2 Standard dedicado ao app e um token S3 com acesso somente a esse bucket, permissões de leitura e gravação de objetos.
2. Copie o endpoint S3 da conta (`https://ACCOUNT_ID.r2.cloudflarestorage.com`), o nome do bucket, o Access Key ID e o Secret Access Key.
3. Habilite um domínio público de leitura para o bucket (domínio `r2.dev` ou domínio próprio) e copie a URL pública. Os vídeos enviados são públicos para reprodução.
4. No Render, configure `Uploads__Provider=s3`, `Uploads__S3Endpoint`, `Uploads__S3Region=auto`, `Uploads__S3Bucket`, `Uploads__S3AccessKeyId`, `Uploads__S3SecretAccessKey` e `Uploads__PublicBaseUrl`.
5. Configure `Uploads__MaxFileSizeBytes=1073741824` (1 GB por arquivo), `Uploads__MaxStorageBytes=9000000000` (limite total padrão de 9 GB) e `Admin__Emails` com os e-mails autorizados para administrar o catálogo.

O app calcula o espaço já usado no bucket antes de cada upload e recusa envios que ultrapassariam o limite configurado. Use um bucket dedicado: objetos gravados por fora do site também contam para esse limite. Isso ajuda a ficar dentro da franquia de armazenamento informada pelo R2, mas não é um teto de cobrança para operações ou tráfego; acompanhe o uso e configure alertas na conta Cloudflare.

#### Deploy pelo GitHub

1. Faça push do projeto para o GitHub e conecte o repositório ao Render.
2. Crie/atualize o serviço pelo Blueprint `render.yaml` e escolha o plano Free para o serviço web.
3. Preencha no Render as variáveis marcadas como `sync: false`, usando os valores do Neon e do R2. Não cole chaves ou connection strings em arquivos do repositório, issues ou mensagens.
4. Inicie o deploy e confira os logs do Render. O health check usa `/`.
5. Teste cadastro/login, criação de catálogo, upload de um vídeo e recarregamento do site. Confirme que a URL do vídeo aponta para o domínio público do bucket.

O `.env.example` contém apenas exemplos. O `.env` real deve permanecer local e ignorado pelo Git. O GitHub é a origem do código e do deploy; não é usado como banco de dados nem como armazenamento runtime de vídeos.

### Painel de administração e envio de vídeos

Configure `Admin__Emails` no ambiente do servidor com os e-mails das contas autorizadas,
separados por vírgula. Cadastre uma conta com um desses e-mails e entre no site; o menu
do perfil mostrará **Gerenciar catálogo**. O painel permite criar, editar e excluir filmes
e séries, manter episódios, enviar vídeos MP4/WebM e escolher o título em destaque.
Não autorize `demo@cinestream.tv`: essa conta de demonstração tem credenciais públicas.

Os vídeos são gravados em `Data/uploads` por padrão no desenvolvimento. No Render, use R2 (`Uploads__Provider=s3`); não use armazenamento local para uploads, pois o filesystem do plano gratuito é efêmero.

---

## 👤 Credenciais de Demonstração

Para testar imediatamente sem precisar preencher o formulário de cadastro:t
- **E-mail:** `demo@cinestream.tv`
- **Senha:** `cine123456`
- *Ou simplesmente clique no botão "Entrar como Usuário Demonstração" no modal de login.*

---

## 📡 Endpoints da API REST

### Administração do Catálogo (exige e-mail autorizado em `Admin__Emails`)
- `GET /api/admin/movies` — Listar títulos para administração
- `POST /api/admin/movies` — Criar um filme ou série
- `PUT /api/admin/movies/{id}` — Editar um título e seus episódios
- `DELETE /api/admin/movies/{id}` — Excluir um título
- `POST /api/admin/videos?fileName={nome}` — Enviar um vídeo MP4/WebM (máximo padrão de 1 GB)

### Autenticação & Perfil
- `POST /api/auth/register` — Cadastro de usuário
- `POST /api/auth/login` — Autenticação de usuário
- `GET /api/auth/me` — Obter dados do usuário autenticado
- `PUT /api/auth/update-profile` — Atualizar nome, avatar e preferências
- `POST /api/auth/change-password` — Alterar senha do usuário
- `POST /api/auth/recover-password` — Recuperação de senha
- `POST /api/auth/logout` — Encerramento de sessão

### Catálogo de Conteúdo
- `GET /api/movies` — Listar todos os títulos com filtros opcionais (`type`, `genre`, `year`, `minScore`, `rating`, `sortBy`)
- `GET /api/movies/featured` — Obter título em destaque do Hero Billboard
- `GET /api/movies/categories` — Obter títulos agrupados por categorias
- `GET /api/movies/continue-watching` — Obter títulos com progresso salvo
- `GET /api/movies/recommendations` — Obter recomendações personalizadas
- `GET /api/movies/{id}` — Obter detalhes completos de um filme ou série
- `GET /api/search?q={query}` — Pesquisa em tempo real

### Interações do Usuário
- `GET /api/watchlist` — Obter títulos salvos na Minha Lista do usuário
- `POST /api/watchlist/{id}` — Adicionar/remover título da Minha Lista
- `POST /api/progress` — Salvar posição e duração de reprodução
- `POST /api/movies/{id}/rate` — Avaliar título com nota de 1 a 5 estrelas
- `POST /api/movies/{id}/like` — Curtir título

---

## 🌐 Deploy em Produção

Para publicar a aplicação para produção (Docker, Linux ou Windows Server):

```bash
dotnet publish -c Release -o ./publish
```

No servidor com Docker ou Linux:
```bash
dotnet ./publish/NetflixClone.dll
```

Ou configure um reverse proxy com **Nginx** ou **Caddy** apontando para a porta do Kestrel.
