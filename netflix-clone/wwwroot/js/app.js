// ==========================================================================
// CINESTREAM - CORE APPLICATION ENGINE
// ==========================================================================

const app = (() => {
  // Application State
  const state = {
    token: localStorage.getItem('cs_token') || null,
    user: null,
    allMovies: [],
    categories: [],
    featuredMovie: null,
    activeMovie: null,
    activeEpisode: null,
    currentView: 'home',
    filters: {
      type: 'all',
      genre: 'all',
      rating: 'all',
      score: 'all',
      sort: 'default'
    },
    searchQuery: '',
    player: {
      isPlaying: false,
      isMuted: false,
      volume: 1,
      playbackRate: 1,
      savedPosition: 0,
      progressInterval: null,
      idleTimeout: null,
      isScrubbing: false
    }
  };

  // Avatar presets for profile & registration
  const AVATARS = [
    'https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=200&q=80',
    'https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?auto=format&fit=crop&w=200&q=80',
    'https://images.unsplash.com/photo-1570295999919-56ceb5ecca61?auto=format&fit=crop&w=200&q=80',
    'https://images.unsplash.com/photo-1580489944761-15a19d654956?auto=format&fit=crop&w=200&q=80',
    'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&w=200&q=80',
    'https://images.unsplash.com/photo-1628157582853-a796fa650a6a?auto=format&fit=crop&w=200&q=80',
    'https://images.unsplash.com/photo-1522075469751-3a6694fb2f61?auto=format&fit=crop&w=200&q=80',
    'https://images.unsplash.com/photo-1544005313-94ddf0286df2?auto=format&fit=crop&w=200&q=80',
    'https://images.unsplash.com/photo-1506794778202-cad84cf45f1d?auto=format&fit=crop&w=200&q=80',
    'https://images.unsplash.com/photo-1517841905240-472988babdf9?auto=format&fit=crop&w=200&q=80',
    'https://images.unsplash.com/photo-1539571696357-5a69c17a67c6?auto=format&fit=crop&w=200&q=80',
    'https://images.unsplash.com/photo-1524504388940-b1c1722653e1?auto=format&fit=crop&w=200&q=80'
  ];

  // DOM Elements cache
  const dom = {};

  // ==========================================================================
  // INITIALIZATION
  // ==========================================================================

  async function init() {
    cacheDomElements();
    setupEventListeners();
    renderAvatarPickers();

    await initAuth();
    await loadFeaturedMovie();
    await loadCategories();
    await loadAllMovies();

    // Check URL hash for direct links (e.g. #movies, #series, #watchlist)
    handleInitialRouting();
  }

  function cacheDomElements() {
    dom.navbar = document.getElementById('navbar');
    dom.logoBtn = document.getElementById('logoBtn');
    dom.authNavContainer = document.getElementById('authNavContainer');

    // Desktop & Mobile Nav
    dom.navLinks = document.querySelectorAll('.desktop-nav .nav-item');
    dom.mobileNavItems = document.querySelectorAll('.mobile-bottom-nav .mobile-nav-item');
    dom.mNavAvatar = document.getElementById('mNavAvatar');

    // Hero
    dom.heroBillboard = document.getElementById('heroBillboard');
    dom.heroBackdrop = document.getElementById('heroBackdrop');
    dom.heroTitle = document.getElementById('heroTitle');
    dom.heroDesc = document.getElementById('heroDesc');
    dom.heroMatch = document.getElementById('heroMatch');
    dom.heroRating = document.getElementById('heroRating');
    dom.heroYear = document.getElementById('heroYear');
    dom.heroDuration = document.getElementById('heroDuration');
    dom.heroMaturityRating = document.getElementById('heroMaturityRating');
    dom.heroPlayBtn = document.getElementById('heroPlayBtn');
    dom.heroInfoBtn = document.getElementById('heroInfoBtn');
    dom.heroWatchlistBtn = document.getElementById('heroWatchlistBtn');
    dom.heroWatchlistIcon = document.getElementById('heroWatchlistIcon');
    dom.heroWatchlistText = document.getElementById('heroWatchlistText');
    dom.heroSoundToggle = document.getElementById('heroSoundToggle');

    // Content views
    dom.mainContent = document.getElementById('mainContent');
    dom.categoriesContainer = document.getElementById('categoriesContainer');
    dom.catalogViewSection = document.getElementById('catalogViewSection');
    dom.catalogGrid = document.getElementById('catalogGrid');
    dom.currentViewTitle = document.getElementById('currentViewTitle');
    dom.filtersResultCount = document.getElementById('filtersResultCount');

    // Filters Bar
    dom.filtersBarSection = document.getElementById('filtersBarSection');
    dom.filterType = document.getElementById('filterType');
    dom.filterGenre = document.getElementById('filterGenre');
    dom.filterRating = document.getElementById('filterRating');
    dom.filterScore = document.getElementById('filterScore');
    dom.filterSort = document.getElementById('filterSort');
    dom.clearFiltersBtn = document.getElementById('clearFiltersBtn');

    // Search
    dom.searchWrapper = document.getElementById('searchWrapper');
    dom.searchBtn = document.getElementById('searchBtn');
    dom.searchInput = document.getElementById('searchInput');
    dom.searchClear = document.getElementById('searchClear');
    dom.searchResultsSection = document.getElementById('searchResultsSection');
    dom.searchResultsGrid = document.getElementById('searchResultsGrid');
    dom.searchResultsTitle = document.getElementById('searchResultsTitle');
    dom.searchEmptyState = document.getElementById('searchEmptyState');
    dom.closeSearchBtn = document.getElementById('closeSearchBtn');

    // Notifications
    dom.notificationsBtn = document.getElementById('notificationsBtn');
    dom.notificationDropdown = document.getElementById('notificationDropdown');

    // Movie Details Modal
    dom.movieModal = document.getElementById('movieModal');
    dom.modalCloseBtn = document.getElementById('modalCloseBtn');
    dom.modalHero = document.getElementById('modalHero');
    dom.modalTitle = document.getElementById('modalTitle');
    dom.modalTypeBadge = document.getElementById('modalTypeBadge');
    dom.modalPlayBtn = document.getElementById('modalPlayBtn');
    dom.modalWatchlistBtn = document.getElementById('modalWatchlistBtn');
    dom.modalWatchlistIcon = document.getElementById('modalWatchlistIcon');
    dom.modalLikeBtn = document.getElementById('modalLikeBtn');
    dom.modalMatch = document.getElementById('modalMatch');
    dom.modalYear = document.getElementById('modalYear');
    dom.modalRating = document.getElementById('modalRating');
    dom.modalDuration = document.getElementById('modalDuration');
    dom.modalSynopsis = document.getElementById('modalSynopsis');
    dom.modalDirector = document.getElementById('modalDirector');
    dom.modalCast = document.getElementById('modalCast');
    dom.modalGenres = document.getElementById('modalGenres');
    dom.modalLikes = document.getElementById('modalLikes');
    dom.starRatingContainer = document.getElementById('starRatingContainer');
    dom.ratingFeedback = document.getElementById('ratingFeedback');
    dom.modalEpisodesSection = document.getElementById('modalEpisodesSection');
    dom.modalEpisodesList = document.getElementById('modalEpisodesList');
    dom.modalSimilarGrid = document.getElementById('modalSimilarGrid');

    // Video Player
    dom.videoPlayerContainer = document.getElementById('videoPlayerContainer');
    dom.mainVideoPlayer = document.getElementById('mainVideoPlayer');
    dom.playerOverlay = document.getElementById('playerOverlay');
    dom.playerBackBtn = document.getElementById('playerBackBtn');
    dom.playerTitle = document.getElementById('playerTitle');
    dom.playerSubtitle = document.getElementById('playerSubtitle');
    dom.playerCenterIcon = document.getElementById('playerCenterIcon');
    dom.playerTimeline = document.getElementById('playerTimeline');
    dom.playerProgressBar = document.getElementById('playerProgressBar');
    dom.timelineTooltip = document.getElementById('timelineTooltip');
    dom.playerPlayPauseBtn = document.getElementById('playerPlayPauseBtn');
    dom.playPauseIcon = document.getElementById('playPauseIcon');
    dom.playerBack10Btn = document.getElementById('playerBack10Btn');
    dom.playerFwd10Btn = document.getElementById('playerFwd10Btn');
    dom.playerMuteBtn = document.getElementById('playerMuteBtn');
    dom.volumeIcon = document.getElementById('volumeIcon');
    dom.playerVolumeSlider = document.getElementById('playerVolumeSlider');
    dom.playerCurrentTime = document.getElementById('playerCurrentTime');
    dom.playerTotalTime = document.getElementById('playerTotalTime');
    dom.playerSpeedSelect = document.getElementById('playerSpeedSelect');
    dom.playerFullscreenBtn = document.getElementById('playerFullscreenBtn');
    dom.playerSubtitleBtn = document.getElementById('playerSubtitleBtn');
    dom.subtitlesMenu = document.getElementById('subtitlesMenu');
    dom.playerQualityBtn = document.getElementById('playerQualityBtn');
    dom.qualityMenu = document.getElementById('qualityMenu');

    // Auth Modals
    dom.loginModal = document.getElementById('loginModal');
    dom.loginCloseBtn = document.getElementById('loginCloseBtn');
    dom.loginForm = document.getElementById('loginForm');
    dom.loginEmail = document.getElementById('loginEmail');
    dom.loginPassword = document.getElementById('loginPassword');
    dom.demoLoginBtn = document.getElementById('demoLoginBtn');
    dom.goToRegisterBtn = document.getElementById('goToRegisterBtn');
    dom.forgotPasswordLink = document.getElementById('forgotPasswordLink');

    dom.registerModal = document.getElementById('registerModal');
    dom.registerCloseBtn = document.getElementById('registerCloseBtn');
    dom.registerForm = document.getElementById('registerForm');
    dom.regName = document.getElementById('regName');
    dom.regEmail = document.getElementById('regEmail');
    dom.regPassword = document.getElementById('regPassword');
    dom.registerAvatarGrid = document.getElementById('registerAvatarGrid');
    dom.goToLoginBtn = document.getElementById('goToLoginBtn');

    dom.recoverModal = document.getElementById('recoverModal');
    dom.recoverCloseBtn = document.getElementById('recoverCloseBtn');
    dom.recoverForm = document.getElementById('recoverForm');
    dom.recoverEmail = document.getElementById('recoverEmail');
    dom.recoverBackToLoginBtn = document.getElementById('recoverBackToLoginBtn');

    // Profile Modal
    dom.profileModal = document.getElementById('profileModal');
    dom.profileCloseBtn = document.getElementById('profileCloseBtn');
    dom.profileHeroAvatar = document.getElementById('profileHeroAvatar');
    dom.profileHeroName = document.getElementById('profileHeroName');
    dom.profileHeroEmail = document.getElementById('profileHeroEmail');
    dom.profileAvatarGrid = document.getElementById('profileAvatarGrid');
    dom.editProfileName = document.getElementById('editProfileName');
    dom.saveProfileGeneralBtn = document.getElementById('saveProfileGeneralBtn');
    dom.currentPassword = document.getElementById('currentPassword');
    dom.newPassword = document.getElementById('newPassword');
    dom.savePasswordBtn = document.getElementById('savePasswordBtn');
    dom.prefAutoPlay = document.getElementById('prefAutoPlay');
    dom.prefQuality = document.getElementById('prefQuality');
    dom.savePrefsBtn = document.getElementById('savePrefsBtn');
    dom.profileHistoryList = document.getElementById('profileHistoryList');
    dom.profileLogoutBtn = document.getElementById('profileLogoutBtn');

    // Toast
    dom.toastNotification = document.getElementById('toastNotification');
    dom.toastMessage = document.getElementById('toastMessage');
  }

  // ==========================================================================
  // AUTHENTICATION & SESSION MANAGEMENT
  // ==========================================================================

  async function initAuth() {
    // If no token exists, auto-login with demo user credentials to give a complete out-of-the-box experience
    if (!state.token) {
      await login('demo@cinestream.tv', 'cine123456', true);
      return;
    }

    try {
      const res = await fetch('/api/auth/me', {
        headers: getAuthHeaders()
      });
      if (res.ok) {
        state.user = await res.json();
        renderAuthNav();
      } else {
        // Token invalid, clear and login demo
        localStorage.removeItem('cs_token');
        state.token = null;
        await login('demo@cinestream.tv', 'cine123456', true);
      }
    } catch {
      renderAuthNav();
    }
  }

  function getAuthHeaders() {
    const headers = { 'Content-Type': 'application/json' };
    if (state.token) {
      headers['Authorization'] = `Bearer ${state.token}`;
    }
    if (state.user && state.user.Id) {
      headers['X-User-Id'] = state.user.Id;
    }
    return headers;
  }

  async function login(email, password, isSilent = false) {
    try {
      const res = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ Email: email, Password: password })
      });
      const data = await res.json();

      if (data.Success) {
        state.token = data.Token;
        state.user = data.User;
        localStorage.setItem('cs_token', data.Token);
        renderAuthNav();
        closeModal(dom.loginModal);
        
        if (!isSilent) {
          showToast(`Bem-vindo de volta, ${state.user.Name}!`);
          await refreshAllContent();
        }
        return true;
      } else {
        if (!isSilent) showToast(data.Message || 'Erro ao realizar login.', 'error');
        return false;
      }
    } catch (err) {
      if (!isSilent) showToast('Erro de conexão ao servidor.', 'error');
      return false;
    }
  }

  async function register(name, email, password, avatar) {
    try {
      const res = await fetch('/api/auth/register', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ Name: name, Email: email, Password: password, Avatar: avatar })
      });
      const data = await res.json();

      if (data.Success) {
        state.token = data.Token;
        state.user = data.User;
        localStorage.setItem('cs_token', data.Token);
        renderAuthNav();
        closeModal(dom.registerModal);
        showToast(`Conta criada com sucesso! Bem-vindo, ${state.user.Name}!`);
        await refreshAllContent();
        return true;
      } else {
        showToast(data.Message || 'Erro no cadastro.', 'error');
        return false;
      }
    } catch {
      showToast('Erro de comunicação com o servidor.', 'error');
      return false;
    }
  }

  async function logout() {
    try {
      await fetch('/api/auth/logout', {
        method: 'POST',
        headers: getAuthHeaders()
      });
    } catch {}

    state.token = null;
    state.user = null;
    localStorage.removeItem('cs_token');
    renderAuthNav();
    closeModal(dom.profileModal);
    showToast('Você saiu da sua conta.');
    await refreshAllContent();
  }

  function renderAuthNav() {
    if (!dom.authNavContainer) return;

    if (state.user) {
      dom.authNavContainer.innerHTML = `
        <div class="profile-menu" id="profileMenu" title="Menu de Perfil">
          <img src="${escapeHtml(state.user.Avatar)}" alt="${escapeHtml(state.user.Name)}" class="profile-avatar">
          <div class="profile-dropdown" id="profileDropdown">
            <div class="profile-dropdown-user">
              <strong>${escapeHtml(state.user.Name)}</strong>
              <span>${escapeHtml(state.user.Email)}</span>
            </div>
            <a class="profile-dropdown-item" onclick="app.openProfileModal('general')">
              <svg width="18" height="18" fill="currentColor" viewBox="0 0 24 24"><path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"/></svg>
              Meu Perfil & Preferências
            </a>
            <a class="profile-dropdown-item" onclick="app.switchView('watchlist')">
              <svg width="18" height="18" fill="currentColor" viewBox="0 0 24 24"><path d="M17 3H7c-1.1 0-1.99.9-1.99 2L5 21l7-3 7 3V5c0-1.1-.9-2-2-2z"/></svg>
              Minha Lista
            </a>
            <a class="profile-dropdown-item" onclick="app.openProfileModal('history')">
              <svg width="18" height="18" fill="currentColor" viewBox="0 0 24 24"><path d="M13 3a9 9 0 0 0-9 9H1l3.89 3.89.07.14L9 12H6c0-3.87 3.13-7 7-7s7 3.13 7 7-3.13 7-7 7c-1.93 0-3.68-.79-4.94-2.06l-1.42 1.42A8.954 8.954 0 0 0 13 21a9 9 0 0 0 0-18zm-1 5v5l4.28 2.54.72-1.21-3.5-2.08V8H12z"/></svg>
              Histórico & Progresso
            </a>
            <div class="profile-divider"></div>
            <a class="profile-dropdown-item" onclick="app.logout()" style="color: #ff4d5a;">
              <svg width="18" height="18" fill="currentColor" viewBox="0 0 24 24"><path d="M17 7l-1.41 1.41L18.17 11H8v2h10.17l-2.58 2.58L17 17l5-5zM4 5h8V3H4c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h8v-2H4V5z"/></svg>
              Sair da Conta
            </a>
          </div>
        </div>
      `;

      if (dom.mNavAvatar) {
        dom.mNavAvatar.src = state.user.Avatar;
      }

      // Dropdown toggle
      const pMenu = document.getElementById('profileMenu');
      const pDrop = document.getElementById('profileDropdown');
      if (pMenu && pDrop) {
        pMenu.onclick = (e) => {
          e.stopPropagation();
          pDrop.classList.toggle('show');
        };
      }
    } else {
      dom.authNavContainer.innerHTML = `
        <div style="display: flex; align-items: center; gap: 8px;">
          <button class="btn btn-sm btn-outline" onclick="app.openLoginModal()">Entrar</button>
          <button class="btn btn-sm btn-primary" onclick="app.openRegisterModal()">Cadastrar</button>
        </div>
      `;
    }
  }

  function renderAvatarPickers() {
    let regHtml = '';
    let profHtml = '';
    AVATARS.forEach((url, i) => {
      const selected = i === 0 ? 'selected' : '';
      regHtml += `<button type="button" class="avatar-choice-btn ${selected}" data-url="${url}" onclick="app.selectAvatar('reg', this)"><img src="${url}" alt="Avatar"></button>`;
      profHtml += `<button type="button" class="avatar-choice-btn ${selected}" data-url="${url}" onclick="app.selectAvatar('prof', this)"><img src="${url}" alt="Avatar"></button>`;
    });
    if (dom.registerAvatarGrid) dom.registerAvatarGrid.innerHTML = regHtml;
    if (dom.profileAvatarGrid) dom.profileAvatarGrid.innerHTML = profHtml;
  }

  function selectAvatar(context, btn) {
    const parent = context === 'reg' ? dom.registerAvatarGrid : dom.profileAvatarGrid;
    parent.querySelectorAll('.avatar-choice-btn').forEach(b => b.classList.remove('selected'));
    btn.classList.add('selected');
    if (context === 'prof' && dom.profileHeroAvatar) {
      dom.profileHeroAvatar.src = btn.getAttribute('data-url');
    }
  }

  // ==========================================================================
  // CATALOG & CONTENT DATA
  // ==========================================================================

  async function loadFeaturedMovie() {
    try {
      const res = await fetch('/api/movies/featured', { headers: getAuthHeaders() });
      if (res.ok) {
        state.featuredMovie = await res.json();
        renderHero(state.featuredMovie);
      }
    } catch (err) {
      console.error('Erro ao carregar destaque:', err);
    }
  }

  async function loadCategories() {
    try {
      const res = await fetch('/api/movies/categories', { headers: getAuthHeaders() });
      if (res.ok) {
        state.categories = await res.json();
        renderCategories(state.categories);
      }
    } catch (err) {
      console.error('Erro ao carregar categorias:', err);
    }
  }

  async function loadAllMovies() {
    try {
      const res = await fetch('/api/movies', { headers: getAuthHeaders() });
      if (res.ok) {
        state.allMovies = await res.json();
      }
    } catch (err) {
      console.error('Erro ao buscar filmes:', err);
    }
  }

  async function refreshAllContent() {
    await loadFeaturedMovie();
    await loadCategories();
    await loadAllMovies();
    if (state.currentView !== 'home') {
      applyFiltersAndRenderCatalog();
    }
  }

  function renderHero(movie) {
    if (!movie) return;

    dom.heroBackdrop.style.backgroundImage = `url('${movie.BackdropUrl}')`;
    dom.heroTitle.textContent = movie.Title;
    dom.heroDesc.textContent = movie.Description;
    dom.heroMatch.textContent = `★ ${movie.Score} (${movie.MatchScore}% relevante)`;
    dom.heroYear.textContent = movie.Year;
    dom.heroDuration.textContent = movie.Duration;
    dom.heroRating.textContent = movie.Rating;
    dom.heroMaturityRating.textContent = movie.Rating;

    // Apply age badge class
    applyRatingClass(dom.heroRating, movie.Rating);

    // Watchlist state on hero
    updateHeroWatchlistButton(movie.IsInWatchlist);

    dom.heroPlayBtn.onclick = () => playVideo(movie.Id);
    dom.heroInfoBtn.onclick = () => openDetailsModal(movie.Id);
    dom.heroWatchlistBtn.onclick = () => toggleWatchlist(movie.Id, true);
  }

  function updateHeroWatchlistButton(isInWatchlist) {
    if (!dom.heroWatchlistBtn) return;
    if (isInWatchlist) {
      dom.heroWatchlistIcon.innerHTML = `<path d="M9 16.2L4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4L9 16.2z"/>`;
      dom.heroWatchlistText.textContent = 'Na Minha Lista';
      dom.heroWatchlistBtn.classList.add('active');
    } else {
      dom.heroWatchlistIcon.innerHTML = `<path d="M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z"/>`;
      dom.heroWatchlistText.textContent = 'Minha Lista';
      dom.heroWatchlistBtn.classList.remove('active');
    }
  }

  function renderCategories(categories) {
    if (!dom.categoriesContainer) return;
    dom.categoriesContainer.innerHTML = '';

    categories.forEach(cat => {
      if (!cat.Movies || cat.Movies.length === 0) return;

      const row = document.createElement('section');
      row.className = 'category-row';
      row.setAttribute('data-cat-id', cat.Id);

      const header = document.createElement('div');
      header.className = 'category-header';
      header.innerHTML = `
        <h2 class="category-title">
          <span>${escapeHtml(cat.Title)}</span>
        </h2>
      `;

      const wrapper = document.createElement('div');
      wrapper.className = 'carousel-viewport-wrapper';

      const arrowLeft = document.createElement('button');
      arrowLeft.className = 'carousel-arrow left';
      arrowLeft.setAttribute('aria-label', 'Rolar para esquerda');
      arrowLeft.innerHTML = `<svg width="28" height="28" fill="currentColor" viewBox="0 0 24 24"><path d="M15.41 7.41L14 6l-6 6 6 6 1.41-1.41L10.83 12z"/></svg>`;

      const arrowRight = document.createElement('button');
      arrowRight.className = 'carousel-arrow right';
      arrowRight.setAttribute('aria-label', 'Rolar para direita');
      arrowRight.innerHTML = `<svg width="28" height="28" fill="currentColor" viewBox="0 0 24 24"><path d="M10 6L8.59 7.41 13.17 12l-4.58 4.59L10 18l6-6z"/></svg>`;

      const track = document.createElement('div');
      track.className = 'carousel-track';

      cat.Movies.forEach(m => {
        track.appendChild(createMovieCard(m));
      });

      arrowLeft.onclick = () => {
        track.scrollBy({ left: -track.clientWidth * 0.75, behavior: 'smooth' });
      };
      arrowRight.onclick = () => {
        track.scrollBy({ left: track.clientWidth * 0.75, behavior: 'smooth' });
      };

      wrapper.appendChild(arrowLeft);
      wrapper.appendChild(track);
      wrapper.appendChild(arrowRight);

      row.appendChild(header);
      row.appendChild(wrapper);
      dom.categoriesContainer.appendChild(row);
    });
  }

  function createMovieCard(movie) {
    const card = document.createElement('div');
    card.className = 'movie-card';
    card.setAttribute('data-id', movie.Id);

    const isWatched = movie.WatchedPercent && movie.WatchedPercent > 0;
    const progressMarkup = isWatched ? `
      <div class="card-watch-progress-bar">
        <div class="card-progress-fill" style="width: ${movie.WatchedPercent}%"></div>
      </div>
    ` : '';

    const badgeTop = movie.Type === 'series' ? 'Série' : 'Filme';

    card.innerHTML = `
      <div class="card-poster-wrap">
        <img class="card-poster" src="${escapeHtml(movie.PosterUrl)}" alt="${escapeHtml(movie.Title)}" loading="lazy">
        <span class="card-badge-top">${badgeTop}</span>
        <span class="card-rating-tag">★ ${movie.Score}</span>
        ${progressMarkup}
      </div>
      <div class="card-overlay-info">
        <h4 class="card-title">${escapeHtml(movie.Title)}</h4>
        <div class="card-meta-row">
          <span style="color: var(--accent-emerald); font-weight: 700;">${movie.MatchScore}%</span>
          <span>${movie.Year}</span>
          <span class="age-badge badge-${movie.Rating.toLowerCase().replace('+', '')}">${movie.Rating}</span>
          <span>${movie.Duration}</span>
        </div>
        <span class="card-genres">${escapeHtml(movie.Genres.slice(0, 2).join(' • '))}</span>
        <div class="card-hover-actions">
          <button class="card-quick-btn play" title="Assistir Agora" onclick="event.stopPropagation(); app.playVideo(${movie.Id});">
            <svg width="16" height="16" fill="currentColor" viewBox="0 0 24 24"><path d="M8 5v14l11-7z"/></svg>
          </button>
          <button class="card-quick-btn watchlist" title="Minha Lista" onclick="event.stopPropagation(); app.toggleWatchlist(${movie.Id});">
            <svg width="16" height="16" fill="currentColor" viewBox="0 0 24 24"><path d="${movie.IsInWatchlist ? 'M9 16.2L4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4L9 16.2z' : 'M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z'}"/></svg>
          </button>
          <button class="card-quick-btn info" title="Mais Informações" onclick="event.stopPropagation(); app.openDetailsModal(${movie.Id});">
            <svg width="16" height="16" fill="currentColor" viewBox="0 0 24 24"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z"/></svg>
          </button>
        </div>
      </div>
    `;

    card.onclick = () => openDetailsModal(movie.Id);
    return card;
  }

  function applyRatingClass(el, rating) {
    el.className = 'age-badge';
    const clean = rating.toLowerCase().replace('+', '');
    el.classList.add(`badge-${clean}`);
  }

  // ==========================================================================
  // DETAILS MODAL & INTERACTIVE RATING
  // ==========================================================================

  async function openDetailsModal(movieId) {
    try {
      const res = await fetch(`/api/movies/${movieId}`, { headers: getAuthHeaders() });
      if (!res.ok) throw new Error('Não encontrado');
      const movie = await res.json();
      state.activeMovie = movie;

      dom.modalHero.style.backgroundImage = `url('${movie.BackdropUrl}')`;
      dom.modalTitle.textContent = movie.Title;
      dom.modalTypeBadge.textContent = movie.Type === 'series' ? 'SÉRIE ORIGINAL' : 'FILME ORIGINAL';
      dom.modalMatch.textContent = `★ ${movie.Score} (${movie.MatchScore}% relevante)`;
      dom.modalYear.textContent = movie.Year;
      dom.modalDuration.textContent = movie.Duration;
      dom.modalRating.textContent = movie.Rating;
      applyRatingClass(dom.modalRating, movie.Rating);
      dom.modalSynopsis.textContent = movie.Description;
      dom.modalDirector.textContent = movie.Director || 'Produção CineStream';
      dom.modalCast.textContent = movie.Cast.join(', ');
      dom.modalGenres.textContent = movie.Genres.join(', ');
      dom.modalLikes.textContent = `${movie.Likes.toLocaleString()} curtidas`;

      // Watchlist toggle state in modal
      updateModalWatchlistIcon(movie.IsInWatchlist);

      // Play button action
      dom.modalPlayBtn.onclick = () => {
        closeModal(dom.movieModal);
        playVideo(movie.Id);
      };

      // Watchlist toggle action
      dom.modalWatchlistBtn.onclick = () => toggleWatchlist(movie.Id);

      // Like button action
      dom.modalLikeBtn.onclick = () => likeMovie(movie.Id);

      // Star rating setup
      setupStarRating(movie);

      // Episodes list (if series)
      if (movie.Type === 'series' && movie.Episodes && movie.Episodes.length > 0) {
        dom.modalEpisodesSection.style.display = 'block';
        renderEpisodesList(movie.Episodes);
      } else {
        dom.modalEpisodesSection.style.display = 'none';
      }

      // Similar titles
      renderSimilarTitles(movie);

      openModal(dom.movieModal);
    } catch (err) {
      showToast('Erro ao carregar detalhes.', 'error');
    }
  }

  function updateModalWatchlistIcon(isInWatchlist) {
    if (isInWatchlist) {
      dom.modalWatchlistIcon.innerHTML = `<path d="M9 16.2L4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4L9 16.2z"/>`;
      dom.modalWatchlistBtn.classList.add('active');
    } else {
      dom.modalWatchlistIcon.innerHTML = `<path d="M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z"/>`;
      dom.modalWatchlistBtn.classList.remove('active');
    }
  }

  function setupStarRating(movie) {
    const starBtns = dom.starRatingContainer.querySelectorAll('.star-btn');
    const userScore = movie.UserRating || 0;

    const highlightStars = (score) => {
      starBtns.forEach(btn => {
        const starVal = parseInt(btn.getAttribute('data-star'));
        btn.classList.toggle('active', starVal <= score);
      });
      dom.ratingFeedback.textContent = score > 0 ? `Sua nota: ${score} de 5 estrelas` : 'Avalie este título';
    };

    highlightStars(userScore);

    starBtns.forEach(btn => {
      btn.onmouseenter = () => highlightStars(parseInt(btn.getAttribute('data-star')));
      btn.onmouseleave = () => highlightStars(movie.UserRating || 0);
      btn.onclick = async () => {
        const score = parseInt(btn.getAttribute('data-star'));
        await rateMovie(movie.Id, score);
        movie.UserRating = score;
        highlightStars(score);
      };
    });
  }

  async function rateMovie(movieId, score) {
    try {
      const res = await fetch(`/api/movies/${movieId}/rate`, {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ Score: score })
      });
      if (res.ok) {
        showToast(`Avaliação de ${score} estrelas registrada!`);
      }
    } catch {}
  }

  async function likeMovie(movieId) {
    try {
      const res = await fetch(`/api/movies/${movieId}/like`, {
        method: 'POST',
        headers: getAuthHeaders()
      });
      if (res.ok) {
        const data = await res.json();
        dom.modalLikes.textContent = `${data.likes.toLocaleString()} curtidas`;
        dom.modalLikeBtn.classList.add('active');
        showToast('Obrigado pelo feedback!');
      }
    } catch {}
  }

  async function toggleWatchlist(movieId, isFromHero = false) {
    try {
      const res = await fetch(`/api/watchlist/${movieId}`, {
        method: 'POST',
        headers: getAuthHeaders()
      });
      if (res.ok) {
        const data = await res.json();
        showToast(data.message);

        // Update active movie cache
        const target = state.allMovies.find(m => m.Id === movieId);
        if (target) target.IsInWatchlist = data.isInWatchlist;

        if (state.featuredMovie && state.featuredMovie.Id === movieId) {
          state.featuredMovie.IsInWatchlist = data.isInWatchlist;
          updateHeroWatchlistButton(data.isInWatchlist);
        }

        if (state.activeMovie && state.activeMovie.Id === movieId) {
          state.activeMovie.IsInWatchlist = data.isInWatchlist;
          updateModalWatchlistIcon(data.isInWatchlist);
        }

        // Refresh categories or catalog if in watchlist view
        if (state.currentView === 'watchlist') {
          applyFiltersAndRenderCatalog();
        } else {
          await loadCategories();
        }
      }
    } catch (err) {
      showToast('Erro ao atualizar Minha Lista.', 'error');
    }
  }

  function renderEpisodesList(episodes) {
    dom.modalEpisodesList.innerHTML = '';
    episodes.forEach(ep => {
      const item = document.createElement('div');
      item.className = 'episode-item';
      item.innerHTML = `
        <span class="ep-number">${ep.Number}</span>
        <div class="ep-thumb-wrap">
          <img src="${escapeHtml(ep.ThumbnailUrl)}" alt="${escapeHtml(ep.Title)}" loading="lazy">
        </div>
        <div class="ep-info">
          <h4>${escapeHtml(ep.Title)}</h4>
          <span class="ep-duration">${escapeHtml(ep.Duration)}</span>
          <p>${escapeHtml(ep.Description)}</p>
        </div>
        <button class="ep-play-btn" title="Assistir este episódio">
          <svg width="18" height="18" fill="currentColor" viewBox="0 0 24 24"><path d="M8 5v14l11-7z"/></svg>
        </button>
      `;

      item.onclick = () => {
        closeModal(dom.movieModal);
        playEpisode(state.activeMovie, ep);
      };

      dom.modalEpisodesList.appendChild(item);
    });
  }

  function renderSimilarTitles(movie) {
    dom.modalSimilarGrid.innerHTML = '';
    const similars = state.allMovies
      .filter(m => m.Id !== movie.Id && m.Genres.some(g => movie.Genres.includes(g)))
      .slice(0, 6);

    similars.forEach(m => {
      const card = createMovieCard(m);
      dom.modalSimilarGrid.appendChild(card);
    });
  }

  // ==========================================================================
  // VIDEO PLAYER ENGINE & CONTINUOUS PROGRESS SAVING
  // ==========================================================================

  function playVideo(movieId) {
    const movie = state.allMovies.find(m => m.Id === movieId) || state.featuredMovie;
    if (!movie) return;

    state.activeMovie = movie;
    state.activeEpisode = null;

    dom.playerTitle.textContent = movie.Title;
    dom.playerSubtitle.textContent = `${movie.Type === 'series' ? 'Série' : 'Filme'} • ${movie.Year} • 4K Ultra HD`;

    startPlayback(movie.VideoUrl, movie.WatchProgressSeconds || 0);
  }

  function playEpisode(series, episode) {
    state.activeMovie = series;
    state.activeEpisode = episode;

    dom.playerTitle.textContent = series.Title;
    dom.playerSubtitle.textContent = `Temporada ${episode.Season} • ${episode.Title} (${episode.Duration})`;

    startPlayback(episode.VideoUrl || series.VideoUrl, 0);
  }

  function startPlayback(url, startSeconds = 0) {
    dom.videoPlayerContainer.classList.add('show');
    dom.mainVideoPlayer.src = url;
    dom.mainVideoPlayer.currentTime = startSeconds;
    dom.mainVideoPlayer.play().catch(() => {});

    state.player.isPlaying = true;
    updatePlayPauseIcon();

    if (startSeconds > 5) {
      showToast(`Continuando reprodução de ${formatTime(startSeconds)}...`);
    }

    // Start auto-saving progress every 3 seconds
    clearInterval(state.player.progressInterval);
    state.player.progressInterval = setInterval(saveCurrentProgress, 3000);

    resetIdleTimer();
  }

  function closePlayer() {
    saveCurrentProgress();
    clearInterval(state.player.progressInterval);

    dom.mainVideoPlayer.pause();
    dom.mainVideoPlayer.src = '';
    dom.videoPlayerContainer.classList.remove('show');
    state.player.isPlaying = false;

    // Refresh categories so "Continuar Assistindo" updates immediately
    loadCategories();
  }

  async function saveCurrentProgress() {
    if (!state.activeMovie || !dom.mainVideoPlayer.duration) return;

    const currentPos = dom.mainVideoPlayer.currentTime;
    const duration = dom.mainVideoPlayer.duration;

    if (currentPos < 3) return;

    try {
      await fetch('/api/progress', {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({
          MovieId: state.activeMovie.Id,
          EpisodeId: state.activeEpisode ? state.activeEpisode.Id : null,
          PositionSeconds: currentPos,
          DurationSeconds: duration
        })
      });
    } catch {}
  }

  function togglePlayPause() {
    if (dom.mainVideoPlayer.paused) {
      dom.mainVideoPlayer.play().catch(() => {});
      state.player.isPlaying = true;
      triggerCenterAnimation('play');
    } else {
      dom.mainVideoPlayer.pause();
      state.player.isPlaying = false;
      triggerCenterAnimation('pause');
      saveCurrentProgress();
    }
    updatePlayPauseIcon();
  }

  function updatePlayPauseIcon() {
    if (dom.mainVideoPlayer.paused) {
      dom.playPauseIcon.innerHTML = `<path d="M8 5v14l11-7z"/>`;
    } else {
      dom.playPauseIcon.innerHTML = `<path d="M6 19h4V5H6v14zm8-14v14h4V5h-4z"/>`;
    }
  }

  function triggerCenterAnimation(action) {
    const svg = dom.playerCenterIcon.querySelector('svg');
    if (action === 'play') {
      svg.innerHTML = `<path d="M8 5v14l11-7z"/>`;
    } else {
      svg.innerHTML = `<path d="M6 19h4V5H6v14zm8-14v14h4V5h-4z"/>`;
    }
    dom.playerCenterIcon.classList.add('animate');
    setTimeout(() => {
      dom.playerCenterIcon.classList.remove('animate');
    }, 450);
  }

  function resetIdleTimer() {
    dom.playerOverlay.classList.remove('idle');
    clearTimeout(state.player.idleTimeout);
    state.player.idleTimeout = setTimeout(() => {
      if (!dom.mainVideoPlayer.paused) {
        dom.playerOverlay.classList.add('idle');
      }
    }, 2800);
  }

  function formatTime(seconds) {
    if (isNaN(seconds) || seconds < 0) return '00:00';
    const mins = Math.floor(seconds / 60);
    const secs = Math.floor(seconds % 60);
    const hrs = Math.floor(mins / 60);
    if (hrs > 0) {
      const remMins = mins % 60;
      return `${hrs}:${remMins < 10 ? '0' : ''}${remMins}:${secs < 10 ? '0' : ''}${secs}`;
    }
    return `${mins < 10 ? '0' : ''}${mins}:${secs < 10 ? '0' : ''}${secs}`;
  }

  // ==========================================================================
  // ADVANCED CATALOG VIEWS & FILTERS
  // ==========================================================================

  function switchView(viewName) {
    state.currentView = viewName;
    window.location.hash = viewName;

    // Update active nav items
    dom.navLinks.forEach(item => {
      item.classList.toggle('active', item.getAttribute('data-view') === viewName);
    });
    dom.mobileNavItems.forEach(item => {
      item.classList.toggle('active', item.getAttribute('data-view') === viewName);
    });

    // Close search if open
    closeSearch();

    if (viewName === 'home') {
      dom.heroBillboard.style.display = 'flex';
      dom.mainContent.style.display = 'flex';
      dom.filtersBarSection.style.display = 'none';
      dom.catalogViewSection.style.display = 'none';
      window.scrollTo({ top: 0, behavior: 'smooth' });
    } else {
      // Show catalog view with filters
      dom.heroBillboard.style.display = 'none';
      dom.mainContent.style.display = 'none';
      dom.filtersBarSection.style.display = 'block';
      dom.catalogViewSection.style.display = 'block';

      if (viewName === 'movies') {
        dom.currentViewTitle.textContent = 'Catálogo de Filmes';
        dom.filterType.value = 'movie';
      } else if (viewName === 'series') {
        dom.currentViewTitle.textContent = 'Séries Originais & Populares';
        dom.filterType.value = 'series';
      } else if (viewName === 'watchlist') {
        dom.currentViewTitle.textContent = 'Minha Lista Pessoal';
        dom.filterType.value = 'all';
      } else {
        dom.currentViewTitle.textContent = 'Explorar Todos os Títulos';
        dom.filterType.value = 'all';
      }

      applyFiltersAndRenderCatalog();
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }

  function filterByGenre(genre) {
    switchView('explore');
    dom.filterGenre.value = genre;
    applyFiltersAndRenderCatalog();
  }

  async function applyFiltersAndRenderCatalog() {
    const type = dom.filterType.value;
    const genre = dom.filterGenre.value;
    const rating = dom.filterRating.value;
    const score = dom.filterScore.value;
    const sort = dom.filterSort.value;

    let filtered = [...state.allMovies];

    // If viewing watchlist only
    if (state.currentView === 'watchlist') {
      filtered = filtered.filter(m => m.IsInWatchlist);
    }

    if (type !== 'all') {
      filtered = filtered.filter(m => m.Type.toLowerCase() === type.toLowerCase());
    }

    if (genre !== 'all') {
      filtered = filtered.filter(m => m.Genres.includes(genre));
    }

    if (rating !== 'all') {
      filtered = filtered.filter(m => m.Rating.toLowerCase() === rating.toLowerCase());
    }

    if (score !== 'all') {
      const minScore = parseFloat(score);
      filtered = filtered.filter(m => m.Score >= minScore);
    }

    // Sorting
    if (sort === 'popular') {
      filtered.sort((a, b) => b.Likes - a.Likes);
    } else if (sort === 'recent') {
      filtered.sort((a, b) => b.Year - a.Year);
    } else if (sort === 'top_rated') {
      filtered.sort((a, b) => b.Score - a.Score);
    } else {
      filtered.sort((a, b) => b.MatchScore - a.MatchScore);
    }

    dom.filtersResultCount.textContent = `${filtered.length} títulos encontrados`;
    dom.catalogGrid.innerHTML = '';

    if (filtered.length === 0) {
      dom.catalogGrid.innerHTML = `
        <div class="empty-state-card" style="grid-column: 1 / -1;">
          <svg width="48" height="48" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0zM10 9v3m0 3h.01"/></svg>
          <h3>Nenhum título corresponde aos filtros aplicados</h3>
          <p>Tente ajustar as opções acima ou limpar os filtros para ver todo o acervo.</p>
          <button class="btn btn-sm btn-primary mt-2" onclick="app.clearFilters()">Limpar Filtros</button>
        </div>
      `;
    } else {
      filtered.forEach(m => {
        dom.catalogGrid.appendChild(createMovieCard(m));
      });
    }
  }

  function clearFilters() {
    dom.filterType.value = 'all';
    dom.filterGenre.value = 'all';
    dom.filterRating.value = 'all';
    dom.filterScore.value = 'all';
    dom.filterSort.value = 'default';
    applyFiltersAndRenderCatalog();
    showToast('Filtros redefinidos');
  }

  // ==========================================================================
  // REAL-TIME SEARCH ENGINE
  // ==========================================================================

  let searchDebounceTimeout = null;

  function handleSearchInput(query) {
    clearTimeout(searchDebounceTimeout);
    state.searchQuery = query.trim();

    if (!state.searchQuery) {
      closeSearch();
      return;
    }

    searchDebounceTimeout = setTimeout(async () => {
      try {
        const res = await fetch(`/api/search?q=${encodeURIComponent(state.searchQuery)}`, { headers: getAuthHeaders() });
        const results = await res.json();
        renderSearchResults(results);
      } catch (err) {
        console.error('Erro na busca:', err);
      }
    }, 220);
  }

  function renderSearchResults(results) {
    dom.searchResultsSection.style.display = 'block';
    dom.heroBillboard.style.display = 'none';
    dom.mainContent.style.display = 'none';
    dom.catalogViewSection.style.display = 'none';
    dom.filtersBarSection.style.display = 'none';

    dom.searchResultsTitle.textContent = `Resultados para "${state.searchQuery}" (${results.length})`;
    dom.searchResultsGrid.innerHTML = '';

    if (results.length === 0) {
      dom.searchResultsGrid.style.display = 'none';
      dom.searchEmptyState.style.display = 'flex';
      dom.searchEmptyMsg.textContent = `Não encontramos nenhum título para "${state.searchQuery}". Experimente buscar por "Ficção", "Ação", "Cyberpunk" ou atores como "Pedro Pascal".`;
    } else {
      dom.searchResultsGrid.style.display = 'grid';
      dom.searchEmptyState.style.display = 'none';
      results.forEach(m => {
        dom.searchResultsGrid.appendChild(createMovieCard(m));
      });
    }
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  function closeSearch() {
    dom.searchResultsSection.style.display = 'none';
    dom.searchInput.value = '';
    dom.searchWrapper.classList.remove('active');
    state.searchQuery = '';

    if (state.currentView === 'home') {
      dom.heroBillboard.style.display = 'flex';
      dom.mainContent.style.display = 'flex';
    } else {
      dom.catalogViewSection.style.display = 'block';
      dom.filtersBarSection.style.display = 'block';
    }
  }

  // ==========================================================================
  // MODAL CONTROLLERS & EVENT LISTENERS
  // ==========================================================================

  function openModal(modalEl) {
    modalEl.classList.add('show');
    document.body.style.overflow = 'hidden';
  }

  function closeModal(modalEl) {
    modalEl.classList.remove('show');
    document.body.style.overflow = '';
  }

  function openLoginModal() {
    closeModal(dom.registerModal);
    closeModal(dom.recoverModal);
    openModal(dom.loginModal);
  }

  function openRegisterModal() {
    closeModal(dom.loginModal);
    closeModal(dom.recoverModal);
    openModal(dom.registerModal);
  }

  function openRecoverModal() {
    closeModal(dom.loginModal);
    openModal(dom.recoverModal);
  }

  function openProfileModal(tab = 'general') {
    if (!state.user) {
      openLoginModal();
      return;
    }

    dom.profileHeroName.textContent = state.user.Name;
    dom.profileHeroEmail.textContent = state.user.Email;
    dom.profileHeroAvatar.src = state.user.Avatar;
    dom.editProfileName.value = state.user.Name;

    // Load history
    renderProfileHistory();

    // Switch to desired tab
    switchProfileTab(tab);
    openModal(dom.profileModal);
  }

  function switchProfileTab(tabName) {
    const tabBtns = dom.profileModal.querySelectorAll('.profile-tab-btn');
    const panes = dom.profileModal.querySelectorAll('.tab-pane');

    tabBtns.forEach(b => {
      b.classList.toggle('active', b.getAttribute('data-tab') === tabName);
    });

    panes.forEach(p => {
      p.classList.toggle('active', p.id === `tab${tabName.charAt(0).toUpperCase() + tabName.slice(1)}`);
    });
  }

  async function renderProfileHistory() {
    try {
      const res = await fetch('/api/movies/continue-watching', { headers: getAuthHeaders() });
      if (res.ok) {
        const list = await res.json();
        dom.profileHistoryList.innerHTML = '';
        if (list.length === 0) {
          dom.profileHistoryList.innerHTML = '<p style="color: var(--text-dim); font-size: 0.85rem;">Nenhum título assistido ainda. Comece a assistir agora!</p>';
        } else {
          list.forEach(m => {
            const item = document.createElement('div');
            item.className = 'history-item';
            item.innerHTML = `
              <img src="${escapeHtml(m.PosterUrl)}" alt="${escapeHtml(m.Title)}">
              <div class="history-item-info">
                <h5>${escapeHtml(m.Title)}</h5>
                <span>${m.WatchedPercent || 0}% assistido</span>
              </div>
              <button class="btn btn-sm btn-primary" onclick="app.closeProfileModal(); app.playVideo(${m.Id});">Continuar</button>
            `;
            dom.profileHistoryList.appendChild(item);
          });
        }
      }
    } catch {}
  }

  function showToast(msg, type = 'success') {
    dom.toastMessage.textContent = msg;
    dom.toastNotification.classList.add('show');
    clearTimeout(dom.toastTimeout);
    dom.toastTimeout = setTimeout(() => {
      dom.toastNotification.classList.remove('show');
    }, 3200);
  }

  function handleInitialRouting() {
    const hash = window.location.hash.replace('#', '');
    if (['movies', 'series', 'watchlist', 'explore'].includes(hash)) {
      switchView(hash);
    }
  }

  function setupEventListeners() {
    // Navbar scroll effect
    window.addEventListener('scroll', () => {
      if (window.scrollY > 35) {
        dom.navbar.classList.add('scrolled');
      } else {
        dom.navbar.classList.remove('scrolled');
      }
    });

    // Logo click: return home
    dom.logoBtn.onclick = (e) => {
      e.preventDefault();
      switchView('home');
    };

    // Nav links click
    dom.navLinks.forEach(link => {
      link.onclick = () => {
        const view = link.getAttribute('data-view');
        switchView(view);
      };
    });

    // Mobile nav clicks
    dom.mobileNavItems.forEach(item => {
      item.onclick = () => {
        const view = item.getAttribute('data-view');
        if (view) {
          switchView(view);
        } else if (item.id === 'mNavProfile') {
          openProfileModal();
        }
      };
    });

    // Search Toggle & Input
    dom.searchBtn.onclick = () => {
      dom.searchWrapper.classList.toggle('active');
      if (dom.searchWrapper.classList.contains('active')) {
        dom.searchInput.focus();
      }
    };

    dom.searchInput.oninput = (e) => handleSearchInput(e.target.value);
    dom.searchClear.onclick = () => closeSearch();
    dom.closeSearchBtn.onclick = () => closeSearch();

    // Notifications toggle
    dom.notificationsBtn.onclick = (e) => {
      e.stopPropagation();
      dom.notificationDropdown.classList.toggle('show');
    };

    // Global click to close popups
    document.addEventListener('click', () => {
      dom.notificationDropdown.classList.remove('show');
      const pDrop = document.getElementById('profileDropdown');
      if (pDrop) pDrop.classList.remove('show');
      dom.subtitlesMenu.classList.remove('show');
      dom.qualityMenu.classList.remove('show');
    });

    // Modal Close Buttons & Backdrop click
    [dom.movieModal, dom.loginModal, dom.registerModal, dom.recoverModal, dom.profileModal].forEach(modal => {
      modal.onclick = (e) => {
        if (e.target === modal) closeModal(modal);
      };
    });

    dom.modalCloseBtn.onclick = () => closeModal(dom.movieModal);
    dom.loginCloseBtn.onclick = () => closeModal(dom.loginModal);
    dom.registerCloseBtn.onclick = () => closeModal(dom.registerModal);
    dom.recoverCloseBtn.onclick = () => closeModal(dom.recoverModal);
    dom.profileCloseBtn.onclick = () => closeModal(dom.profileModal);

    // Filter controls
    [dom.filterType, dom.filterGenre, dom.filterRating, dom.filterScore, dom.filterSort].forEach(sel => {
      sel.onchange = () => applyFiltersAndRenderCatalog();
    });
    dom.clearFiltersBtn.onclick = () => clearFilters();

    // Auth forms
    dom.loginForm.onsubmit = async () => {
      await login(dom.loginEmail.value, dom.loginPassword.value);
    };

    dom.demoLoginBtn.onclick = async () => {
      await login('demo@cinestream.tv', 'cine123456');
    };

    dom.goToRegisterBtn.onclick = () => openRegisterModal();
    dom.goToLoginBtn.onclick = () => openLoginModal();
    dom.forgotPasswordLink.onclick = (e) => {
      e.preventDefault();
      openRecoverModal();
    };
    dom.recoverBackToLoginBtn.onclick = () => openLoginModal();

    dom.registerForm.onsubmit = async () => {
      const selectedAvatarBtn = dom.registerAvatarGrid.querySelector('.avatar-choice-btn.selected');
      const avatarUrl = selectedAvatarBtn ? selectedAvatarBtn.getAttribute('data-url') : AVATARS[0];
      await register(dom.regName.value, dom.regEmail.value, dom.regPassword.value, avatarUrl);
    };

    dom.recoverForm.onsubmit = async () => {
      try {
        const res = await fetch('/api/auth/recover-password', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ Email: dom.recoverEmail.value })
        });
        const data = await res.json();
        showToast(data.Message || 'Instruções enviadas.');
        closeModal(dom.recoverModal);
      } catch {}
    };

    // Profile Modal Tabs
    dom.profileModal.querySelectorAll('.profile-tab-btn').forEach(btn => {
      btn.onclick = () => switchProfileTab(btn.getAttribute('data-tab'));
    });

    // Save profile general
    dom.saveProfileGeneralBtn.onclick = async () => {
      const newName = dom.editProfileName.value.trim();
      const selectedAvatarBtn = dom.profileAvatarGrid.querySelector('.avatar-choice-btn.selected');
      const avatarUrl = selectedAvatarBtn ? selectedAvatarBtn.getAttribute('data-url') : state.user.Avatar;

      try {
        const res = await fetch('/api/auth/update-profile', {
          method: 'PUT',
          headers: getAuthHeaders(),
          body: JSON.stringify({ Name: newName, Avatar: avatarUrl, Preferences: state.user.Preferences })
        });
        const data = await res.json();
        if (data.Success) {
          state.user = data.User;
          renderAuthNav();
          showToast('Perfil atualizado com sucesso!');
        }
      } catch {}
    };

    // Save profile password
    dom.savePasswordBtn.onclick = async () => {
      const curr = dom.currentPassword.value;
      const next = dom.newPassword.value;

      try {
        const res = await fetch('/api/auth/change-password', {
          method: 'POST',
          headers: getAuthHeaders(),
          body: JSON.stringify({ CurrentPassword: curr, NewPassword: next })
        });
        const data = await res.json();
        if (data.Success) {
          showToast('Senha alterada com sucesso!');
          dom.currentPassword.value = '';
          dom.newPassword.value = '';
        } else {
          showToast(data.Message, 'error');
        }
      } catch {}
    };

    // Save playback prefs
    dom.savePrefsBtn.onclick = async () => {
      state.user.Preferences = {
        AutoPlayNextEpisode: dom.prefAutoPlay.checked,
        PreferredQuality: dom.prefQuality.value,
        PreferredAudioLanguage: 'Português (Brasil)',
        PreferredSubtitleLanguage: 'Português (Brasil)'
      };
      try {
        await fetch('/api/auth/update-profile', {
          method: 'PUT',
          headers: getAuthHeaders(),
          body: JSON.stringify({ Name: state.user.Name, Avatar: state.user.Avatar, Preferences: state.user.Preferences })
        });
        showToast('Preferências salvas com sucesso!');
      } catch {}
    };

    dom.profileLogoutBtn.onclick = () => logout();

    // ==========================================
    // Video Player Controls
    // ==========================================
    dom.playerBackBtn.onclick = () => closePlayer();
    dom.playerPlayPauseBtn.onclick = () => togglePlayPause();

    dom.mainVideoPlayer.onclick = () => togglePlayPause();
    dom.mainVideoPlayer.ondblclick = () => toggleFullscreen();

    dom.mainVideoPlayer.ontimeupdate = () => {
      if (state.player.isScrubbing) return;
      const cur = dom.mainVideoPlayer.currentTime;
      const dur = dom.mainVideoPlayer.duration || 0;
      dom.playerCurrentTime.textContent = formatTime(cur);
      dom.playerTotalTime.textContent = formatTime(dur);
      const pct = dur > 0 ? (cur / dur) * 100 : 0;
      dom.playerProgressBar.style.width = `${pct}%`;
    };

    dom.mainVideoPlayer.onended = () => {
      updatePlayPauseIcon();
      saveCurrentProgress();
      showToast('Reprodução concluída.');
    };

    // Seek scrubber
    dom.playerTimeline.onclick = (e) => {
      const rect = dom.playerTimeline.getBoundingClientRect();
      const pos = (e.clientX - rect.left) / rect.width;
      const targetTime = pos * dom.mainVideoPlayer.duration;
      dom.mainVideoPlayer.currentTime = targetTime;
    };

    dom.playerTimeline.onmousemove = (e) => {
      const rect = dom.playerTimeline.getBoundingClientRect();
      const pos = Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
      const targetTime = pos * (dom.mainVideoPlayer.duration || 0);
      dom.timelineTooltip.textContent = formatTime(targetTime);
      dom.timelineTooltip.style.left = `${pos * 100}%`;
      dom.timelineTooltip.style.display = 'block';
    };
    dom.playerTimeline.onmouseleave = () => {
      dom.timelineTooltip.style.display = 'none';
    };

    // 10s skip
    dom.playerBack10Btn.onclick = () => {
      dom.mainVideoPlayer.currentTime = Math.max(0, dom.mainVideoPlayer.currentTime - 10);
      showToast('-10 segundos');
    };
    dom.playerFwd10Btn.onclick = () => {
      dom.mainVideoPlayer.currentTime = Math.min(dom.mainVideoPlayer.duration, dom.mainVideoPlayer.currentTime + 10);
      showToast('+10 segundos');
    };

    // Volume & Mute
    dom.playerMuteBtn.onclick = () => {
      dom.mainVideoPlayer.muted = !dom.mainVideoPlayer.muted;
      updateVolumeUI();
    };
    dom.playerVolumeSlider.oninput = (e) => {
      const val = parseFloat(e.target.value);
      dom.mainVideoPlayer.volume = val;
      dom.mainVideoPlayer.muted = val === 0;
      updateVolumeUI();
    };

    // Speed
    dom.playerSpeedSelect.onchange = (e) => {
      dom.mainVideoPlayer.playbackRate = parseFloat(e.target.value);
      showToast(`Velocidade: ${e.target.value}x`);
    };

    // Subtitles menu
    dom.playerSubtitleBtn.onclick = (e) => {
      e.stopPropagation();
      dom.subtitlesMenu.classList.toggle('show');
    };
    dom.subtitlesMenu.querySelectorAll('input').forEach(radio => {
      radio.onchange = (e) => {
        showToast(`Legendas: ${e.target.parentNode.textContent.trim()}`);
        dom.subtitlesMenu.classList.remove('show');
      };
    });

    // Quality menu
    dom.playerQualityBtn.onclick = (e) => {
      e.stopPropagation();
      dom.qualityMenu.classList.toggle('show');
    };
    dom.qualityMenu.querySelectorAll('input').forEach(radio => {
      radio.onchange = (e) => {
        showToast(`Qualidade: ${e.target.parentNode.textContent.trim()}`);
        dom.qualityMenu.classList.remove('show');
      };
    });

    // Fullscreen
    dom.playerFullscreenBtn.onclick = () => toggleFullscreen();

    // Idle mouse in player
    dom.videoPlayerContainer.onmousemove = () => resetIdleTimer();

    // Keyboard Shortcuts
    window.addEventListener('keydown', (e) => {
      // Ignore if typing in an input
      if (['INPUT', 'TEXTAREA'].includes(document.activeElement.tagName)) return;

      if (dom.videoPlayerContainer.classList.contains('show')) {
        if (e.code === 'Space' || e.code === 'KeyK') {
          e.preventDefault();
          togglePlayPause();
        } else if (e.code === 'KeyF') {
          e.preventDefault();
          toggleFullscreen();
        } else if (e.code === 'KeyM') {
          e.preventDefault();
          dom.mainVideoPlayer.muted = !dom.mainVideoPlayer.muted;
          updateVolumeUI();
        } else if (e.code === 'ArrowLeft' || e.code === 'KeyJ') {
          e.preventDefault();
          dom.mainVideoPlayer.currentTime = Math.max(0, dom.mainVideoPlayer.currentTime - 10);
        } else if (e.code === 'ArrowRight' || e.code === 'KeyL') {
          e.preventDefault();
          dom.mainVideoPlayer.currentTime = Math.min(dom.mainVideoPlayer.duration, dom.mainVideoPlayer.currentTime + 10);
        } else if (e.code === 'ArrowUp') {
          e.preventDefault();
          dom.mainVideoPlayer.volume = Math.min(1, dom.mainVideoPlayer.volume + 0.1);
          dom.playerVolumeSlider.value = dom.mainVideoPlayer.volume;
          updateVolumeUI();
        } else if (e.code === 'ArrowDown') {
          e.preventDefault();
          dom.mainVideoPlayer.volume = Math.max(0, dom.mainVideoPlayer.volume - 0.1);
          dom.playerVolumeSlider.value = dom.mainVideoPlayer.volume;
          updateVolumeUI();
        } else if (e.code === 'Escape') {
          closePlayer();
        }
      } else {
        if (e.code === 'Escape') {
          closeModal(dom.movieModal);
          closeModal(dom.loginModal);
          closeModal(dom.registerModal);
          closeModal(dom.profileModal);
        } else if ((e.ctrlKey || e.metaKey) && e.code === 'KeyK') {
          e.preventDefault();
          dom.searchWrapper.classList.add('active');
          dom.searchInput.focus();
        }
      }
    });

    // Save on tab close
    window.addEventListener('beforeunload', () => {
      saveCurrentProgress();
    });
  }

  function updateVolumeUI() {
    if (dom.mainVideoPlayer.muted || dom.mainVideoPlayer.volume === 0) {
      dom.volumeIcon.innerHTML = `<path d="M16.5 12c0-1.77-1.02-3.29-2.5-4.03v2.21l2.45 2.45c.03-.2.05-.41.05-.63zm2.5 0c0 .94-.2 1.82-.54 2.64l1.51 1.51C20.63 14.91 21 13.5 21 12c0-4.28-2.99-7.86-7-8.77v2.06c2.89.86 5 3.54 5 6.71zM4.27 3L3 4.27l4.73 4.73H3v6h4l5 5v-6.73l4.25 4.25c-.67.52-1.42.93-2.25 1.18v2.06c1.38-.31 2.63-.95 3.69-1.81L19.73 21 21 19.73l-9-9L4.27 3zM12 4L9.91 6.09 12 8.18V4z"/>`;
    } else {
      dom.volumeIcon.innerHTML = `<path d="M3 9v6h4l5 5V4L7 9H3zm13.5 3c0-1.77-1.02-3.29-2.5-4.03v8.05c1.48-.73 2.5-2.25 2.5-4.02zM14 3.23v2.06c2.89.86 5 3.54 5 6.71s-2.11 5.85-5 6.71v2.06c4.01-.91 7-4.49 7-8.77s-2.99-7.86-7-8.77z"/>`;
    }
  }

  function toggleFullscreen() {
    if (!document.fullscreenElement) {
      dom.videoPlayerContainer.requestFullscreen().catch(() => {});
    } else {
      document.exitFullscreen().catch(() => {});
    }
  }

  function escapeHtml(str) {
    if (!str) return '';
    return str
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }

  // Export public interface
  return {
    init,
    switchView,
    filterByGenre,
    clearFilters,
    playVideo,
    playEpisode,
    openDetailsModal,
    toggleWatchlist,
    rateMovie,
    likeMovie,
    selectAvatar,
    openLoginModal,
    openRegisterModal,
    openProfileModal,
    closeProfileModal: () => closeModal(dom.profileModal),
    logout,
    showToast
  };
})();

// Start application when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
  app.init();
});
