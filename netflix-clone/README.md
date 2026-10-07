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
- **Banco de Dados**: SQLite (`Data/cinestream.db`) para contas, catálogo e dados dos usuários
- **Vídeos**: Streams em MP4 abertos e compatíveis com todos os navegadores modernos

---

## 🚀 Como Executar Localmente

### Pré-requisitos
- .NET SDK 8.0, 9.0 ou 10.0 instalado ([Download .NET](https://dotnet.microsoft.com/download))

### Passo a Passo

1. **Abra o terminal no diretório do projeto:**
   ```bash
   cd c:\Users\fic\.gemini\antigravity-ide\scratch\netflix-clone
   ```

2. **Compile a aplicação:**
   ```bash
   dotnet build
   ```

3. **Execute o servidor:**
   ```bash
   dotnet run
   ```

4. **Acesse no seu navegador:**
   Abra [http://localhost:5167](http://localhost:5167)

O banco SQLite é criado automaticamente na primeira execução. Os dados de uma instalação
anterior em `Data/cinestream_data.json` são importados automaticamente. Para escolher outro
caminho para o arquivo do banco, configure a variável de ambiente `Database__Path`;
em produção, use um volume persistente para que o banco sobreviva a novos deploys.

### Painel de administração e envio de vídeos

Configure `Admin__Emails` no ambiente do servidor com os e-mails das contas autorizadas,
separados por vírgula. Cadastre uma conta com um desses e-mails e entre no site; o menu
do perfil mostrará **Gerenciar catálogo**. O painel permite criar, editar e excluir filmes
e séries, manter episódios, enviar vídeos MP4/WebM e escolher o título em destaque.
Não autorize `demo@cinestream.tv`: essa conta de demonstração tem credenciais públicas.

Os vídeos são gravados em `Data/uploads` por padrão. O limite padrão é 1 GB por arquivo;
configure `Uploads__MaxFileSizeBytes` para alterar. Para hospedagem em contêiner, monte um
volume persistente e configure `Uploads__Directory` para o caminho do volume. Por exemplo,
em Render, monte o disco em `/var/data` e defina `Database__Path=/var/data/cinestream.db`,
`Uploads__Directory=/var/data/uploads` e `Admin__Emails=seu-email@exemplo.com`.
O disco efêmero do plano gratuito não preserva vídeos nem banco após reinícios/deploys.

---

## 👤 Credenciais de Demonstração

Para testar imediatamente sem precisar preencher o formulário de cadastro:
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
