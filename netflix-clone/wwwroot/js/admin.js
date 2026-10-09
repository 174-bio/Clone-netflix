(() => {
  const token = localStorage.getItem('cs_token');
  const elements = {
    newMovieBtn: document.getElementById('newMovieBtn'),
    notice: document.getElementById('adminNotice'),
    catalogCount: document.getElementById('catalogCount'),
    catalogList: document.getElementById('catalogList'),
    catalogSearch: document.getElementById('catalogSearch'),
    storageUsed: document.getElementById('storageUsed'),
    storageRemaining: document.getElementById('storageRemaining'),
    storageUsageProgress: document.getElementById('storageUsageProgress'),
    editor: document.getElementById('movieEditor'),
    editorTitle: document.getElementById('editorTitle'),
    form: document.getElementById('movieForm'),
    movieId: document.getElementById('movieId'),
    movieTitle: document.getElementById('movieTitle'),
    movieType: document.getElementById('movieType'),
    movieYear: document.getElementById('movieYear'),
    movieDuration: document.getElementById('movieDuration'),
    movieRating: document.getElementById('movieRating'),
    movieScore: document.getElementById('movieScore'),
    movieMatchScore: document.getElementById('movieMatchScore'),
    movieDescription: document.getElementById('movieDescription'),
    moviePoster: document.getElementById('moviePoster'),
    movieBackdrop: document.getElementById('movieBackdrop'),
    movieDirector: document.getElementById('movieDirector'),
    movieGenres: document.getElementById('movieGenres'),
    movieCast: document.getElementById('movieCast'),
    movieVideoUrl: document.getElementById('movieVideoUrl'),
    movieFeatured: document.getElementById('movieFeatured'),
    episodesSection: document.getElementById('episodesSection'),
    episodeList: document.getElementById('episodeList'),
    saveMovieBtn: document.getElementById('saveMovieBtn')
  };

  let catalog = [];

  function authHeaders() {
    return { Authorization: `Bearer ${token}` };
  }

  function showNotice(message, isSuccess = false) {
    elements.notice.textContent = message;
    elements.notice.classList.toggle('is-success', isSuccess);
    elements.notice.hidden = false;
    elements.notice.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
  }

  async function readResponse(response) {
    const data = await response.json().catch(() => ({}));
    if (!response.ok) {
      if (response.status === 401) {
        throw new Error('Acesso restrito. Entre com uma conta autorizada para administrar o catálogo.');
      }
      throw new Error(data.Message || data.message || 'Não foi possível concluir a operação.');
    }
    return data;
  }

  async function apiRequest(path, options = {}) {
    const response = await fetch(path, {
      ...options,
      headers: { ...authHeaders(), ...options.headers }
    });
    return readResponse(response);
  }

  function escapeHtml(value) {
    return String(value ?? '').replace(/[&<>"']/g, character => ({
      '&': '&amp;',
      '<': '&lt;',
      '>': '&gt;',
      '"': '&quot;',
      "'": '&#039;'
    })[character]);
  }

  function renderCatalog() {
    const query = elements.catalogSearch.value.trim().toLocaleLowerCase('pt-BR');
    const visibleCatalog = catalog.filter(movie => movie.Title.toLocaleLowerCase('pt-BR').includes(query));
    elements.catalogCount.textContent = `${visibleCatalog.length} de ${catalog.length} títulos`;
    elements.catalogList.replaceChildren();

    if (visibleCatalog.length === 0) {
      const empty = document.createElement('p');
      empty.className = 'admin-empty';
      empty.textContent = catalog.length ? 'Nenhum título encontrado.' : 'Seu catálogo ainda está vazio. Adicione o primeiro título.';
      elements.catalogList.appendChild(empty);
      return;
    }

    for (const movie of visibleCatalog) {
      const row = document.createElement('article');
      row.className = 'admin-title-row';

      const info = document.createElement('div');
      info.className = 'admin-title-info';
      const poster = document.createElement('img');
      poster.src = movie.PosterUrl;
      poster.alt = '';
      poster.loading = 'lazy';
      const text = document.createElement('div');
      const title = document.createElement('strong');
      title.textContent = movie.Title;
      const details = document.createElement('span');
      details.textContent = `${movie.Type === 'series' ? 'Série' : 'Filme'} · ${movie.Year}${movie.IsFeatured ? ' · Em destaque' : ''}`;
      text.append(title, details);
      info.append(poster, text);

      const actions = document.createElement('div');
      actions.className = 'admin-title-actions';
      const editButton = document.createElement('button');
      editButton.type = 'button';
      editButton.className = 'btn btn-outline';
      editButton.textContent = 'Editar';
      editButton.addEventListener('click', () => editMovie(movie.Id));
      const deleteButton = document.createElement('button');
      deleteButton.type = 'button';
      deleteButton.className = 'btn btn-danger-outline';
      deleteButton.textContent = 'Excluir';
      deleteButton.addEventListener('click', () => deleteMovie(movie));
      actions.append(editButton, deleteButton);
      row.append(info, actions);
      elements.catalogList.appendChild(row);
    }
  }

  function episodeMarkup(episode = {}) {
    return `
      <article class="admin-episode-row">
        <div class="admin-episode-heading">
          <strong>Episódio</strong>
          <button class="btn btn-danger-outline remove-episode-btn" type="button">Remover</button>
        </div>
        <div class="admin-episode-fields">
          <label class="admin-field"><span>Título do episódio</span><input data-episode="Title" maxlength="150" required value="${escapeHtml(episode.Title)}"></label>
          <label class="admin-field"><span>Duração</span><input data-episode="Duration" maxlength="60" placeholder="45 min" value="${escapeHtml(episode.Duration)}"></label>
          <label class="admin-field"><span>Temporada</span><input data-episode="Season" type="number" min="1" value="${episode.Season || 1}" required></label>
          <label class="admin-field"><span>Número</span><input data-episode="Number" type="number" min="1" value="${episode.Number || 1}" required></label>
          <label class="admin-field admin-field-wide"><span>Sinopse do episódio</span><textarea data-episode="Description" maxlength="2000" rows="2">${escapeHtml(episode.Description)}</textarea></label>
          <label class="admin-field"><span>URL da miniatura</span><input data-episode="ThumbnailUrl" type="url" maxlength="2048" placeholder="https://…" value="${escapeHtml(episode.ThumbnailUrl)}"></label>
          <div class="admin-field">
            <span>Vídeo do episódio</span>
            <div class="admin-upload-row">
              <input data-episode="VideoUrl" type="text" maxlength="2048" placeholder="URL do vídeo" value="${escapeHtml(episode.VideoUrl)}">
              <input class="admin-file-input" data-episode-file type="file" accept="video/mp4,video/webm,.mp4,.webm">
              <button class="btn btn-outline upload-video-btn" type="button" data-url-selector="[data-episode='VideoUrl']">Enviar</button>
            </div>
            <progress class="admin-upload-progress" max="100" value="0" hidden></progress>
          </div>
        </div>
      </article>`;
  }

  function resetEditor() {
    elements.form.reset();
    elements.movieId.value = '';
    elements.movieYear.value = new Date().getFullYear();
    elements.movieDuration.value = '';
    elements.movieRating.value = '16+';
    elements.movieScore.value = '8.0';
    elements.movieMatchScore.value = '90';
    elements.episodeList.replaceChildren();
    elements.editorTitle.textContent = 'Novo título';
    elements.saveMovieBtn.textContent = 'Salvar título';
    updateEpisodeSection();
  }

  function updateEpisodeSection() {
    elements.episodesSection.hidden = elements.movieType.value !== 'series';
  }

  function editMovie(id) {
    const movie = catalog.find(item => item.Id === id);
    if (!movie) return;
    elements.movieId.value = movie.Id;
    elements.movieTitle.value = movie.Title;
    elements.movieType.value = movie.Type;
    elements.movieYear.value = movie.Year;
    elements.movieDuration.value = movie.Duration;
    elements.movieRating.value = movie.Rating;
    elements.movieScore.value = movie.Score;
    elements.movieMatchScore.value = movie.MatchScore;
    elements.movieDescription.value = movie.Description;
    elements.moviePoster.value = movie.PosterUrl;
    elements.movieBackdrop.value = movie.BackdropUrl;
    elements.movieDirector.value = movie.Director;
    elements.movieGenres.value = movie.Genres.join(', ');
    elements.movieCast.value = movie.Cast.join(', ');
    elements.movieVideoUrl.value = movie.VideoUrl;
    elements.movieFeatured.checked = movie.IsFeatured;
    elements.episodeList.innerHTML = (movie.Episodes || []).map(episodeMarkup).join('');
    elements.editorTitle.textContent = `Editar: ${movie.Title}`;
    elements.saveMovieBtn.textContent = 'Salvar alterações';
    updateEpisodeSection();
    elements.editor.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  async function loadCatalog() {
    catalog = await apiRequest('/api/admin/movies');
    renderCatalog();
  }

  function renderStorageUsage(usage) {
    const usedBytes = Number(usage.UsedBytes);
    const maxBytes = Number(usage.MaxBytes);
    if (!Number.isFinite(usedBytes) || !Number.isFinite(maxBytes) || usedBytes < 0 || maxBytes <= 0) {
      throw new Error('O servidor retornou informações inválidas de armazenamento.');
    }

    const remainingBytes = Math.max(0, maxBytes - usedBytes);
    const percentage = Math.min(100, (usedBytes / maxBytes) * 100);
    const gigabytes = bytes => `${(bytes / 1_000_000_000).toFixed(2)} GB`;
    elements.storageUsed.textContent = `${gigabytes(usedBytes)} usados de ${gigabytes(maxBytes)}`;
    elements.storageRemaining.textContent = `${gigabytes(remainingBytes)} disponíveis`;
    elements.storageUsageProgress.value = percentage;
    elements.storageUsageProgress.setAttribute(
      'aria-valuetext',
      `${gigabytes(usedBytes)} usados; ${gigabytes(remainingBytes)} disponíveis`
    );
  }

  async function loadStorageUsage() {
    const usage = await apiRequest('/api/admin/videos/storage');
    renderStorageUsage(usage);
  }

  async function deleteMovie(movie) {
    if (!window.confirm(`Excluir "${movie.Title}" do catálogo? Esta ação não pode ser desfeita.`)) return;
    try {
      await apiRequest(`/api/admin/movies/${movie.Id}`, { method: 'DELETE' });
      await loadCatalog();
      await loadStorageUsage();
      showNotice(`"${movie.Title}" foi removido do catálogo.`, true);
      if (Number(elements.movieId.value) === movie.Id) resetEditor();
    } catch (error) {
      showNotice(error.message);
    }
  }

  function uploadVideo(button) {
    const row = button.closest('.admin-field');
    const fileInput = button.dataset.fileInput
      ? document.getElementById(button.dataset.fileInput)
      : row.querySelector('[data-episode-file]');
    const urlInput = button.dataset.urlInput
      ? document.getElementById(button.dataset.urlInput)
      : button.closest('.admin-episode-row').querySelector(button.dataset.urlSelector);
    const progress = row.querySelector('progress');
    const file = fileInput.files[0];

    if (!file) {
      showNotice('Selecione um arquivo MP4 ou WebM antes de enviar.');
      return;
    }

    const request = new XMLHttpRequest();
    button.disabled = true;
    progress.hidden = false;
    progress.value = 0;
    request.open('POST', `/api/admin/videos?fileName=${encodeURIComponent(file.name)}`);
    request.setRequestHeader('Authorization', `Bearer ${token}`);
    request.setRequestHeader('Content-Type', 'application/octet-stream');
    request.upload.onprogress = event => {
      if (event.lengthComputable) progress.value = Math.round((event.loaded / event.total) * 100);
    };
    request.onload = () => {
      button.disabled = false;
      try {
        const data = JSON.parse(request.responseText);
        if (request.status < 200 || request.status >= 300) throw new Error(data.Message || 'Falha ao enviar o vídeo.');
        urlInput.value = data.Url;
        fileInput.value = '';
        progress.value = 100;
        if (data.StorageUsage) {
          renderStorageUsage(data.StorageUsage);
          showNotice('Vídeo enviado com sucesso.', true);
        } else {
          loadStorageUsage()
            .then(() => showNotice('Vídeo enviado com sucesso.', true))
            .catch(() => showNotice('Vídeo enviado, mas não foi possível atualizar o uso do armazenamento.'));
        }
      } catch (error) {
        showNotice(error.message || 'Resposta inválida do servidor.');
      }
    };
    request.onerror = () => {
      button.disabled = false;
      showNotice('Erro de conexão durante o envio do vídeo.');
    };
    request.onabort = () => {
      button.disabled = false;
      showNotice('Envio do vídeo cancelado.');
    };
    request.send(file);
  }

  function splitList(value) {
    return value.split(',').map(item => item.trim()).filter(Boolean);
  }

  function collectEpisodes() {
    if (elements.movieType.value !== 'series') return [];
    return [...elements.episodeList.querySelectorAll('.admin-episode-row')].map(row => {
      const value = name => row.querySelector(`[data-episode="${name}"]`).value.trim();
      return {
        Season: Number(value('Season')),
        Number: Number(value('Number')),
        Title: value('Title'),
        Description: value('Description'),
        Duration: value('Duration'),
        ThumbnailUrl: value('ThumbnailUrl'),
        VideoUrl: value('VideoUrl')
      };
    });
  }

  async function saveMovie(event) {
    event.preventDefault();
    if (elements.form.querySelector('.upload-video-btn:disabled')) {
      showNotice('Aguarde o término do envio do vídeo.');
      return;
    }

    const movie = {
      Title: elements.movieTitle.value.trim(),
      Description: elements.movieDescription.value.trim(),
      Type: elements.movieType.value,
      BackdropUrl: elements.movieBackdrop.value.trim(),
      PosterUrl: elements.moviePoster.value.trim(),
      VideoUrl: elements.movieVideoUrl.value.trim(),
      Year: Number(elements.movieYear.value),
      Rating: elements.movieRating.value,
      Score: Number(elements.movieScore.value),
      MatchScore: Number(elements.movieMatchScore.value),
      Duration: elements.movieDuration.value.trim(),
      Genres: splitList(elements.movieGenres.value),
      Cast: splitList(elements.movieCast.value),
      Director: elements.movieDirector.value.trim(),
      IsFeatured: elements.movieFeatured.checked,
      Episodes: collectEpisodes()
    };
    const id = elements.movieId.value;
    elements.saveMovieBtn.disabled = true;
    try {
      await apiRequest(id ? `/api/admin/movies/${id}` : '/api/admin/movies', {
        method: id ? 'PUT' : 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(movie)
      });
      await loadCatalog();
      resetEditor();
      showNotice('Catálogo atualizado com sucesso.', true);
    } catch (error) {
      showNotice(error.message);
    } finally {
      elements.saveMovieBtn.disabled = false;
    }
  }

  async function init() {
    if (!token) {
      window.location.replace('/');
      return;
    }

    const results = await Promise.allSettled([loadCatalog(), loadStorageUsage()]);
    for (const result of results) {
      if (result.status === 'rejected') {
        showNotice(result.reason.message || 'Não foi possível carregar os dados do painel.');
        if (/não autorizado|unauthorized/i.test(result.reason.message)) {
          window.setTimeout(() => window.location.replace('/'), 1500);
          break;
        }
      }
    }

    elements.newMovieBtn.addEventListener('click', () => {
      resetEditor();
      elements.editor.scrollIntoView({ behavior: 'smooth', block: 'start' });
      elements.movieTitle.focus();
    });
    document.getElementById('cancelEditBtn').addEventListener('click', resetEditor);
    document.getElementById('resetFormBtn').addEventListener('click', resetEditor);
    document.getElementById('addEpisodeBtn').addEventListener('click', () => {
      elements.episodeList.insertAdjacentHTML('beforeend', episodeMarkup());
    });
    elements.movieType.addEventListener('change', updateEpisodeSection);
    elements.catalogSearch.addEventListener('input', renderCatalog);
    elements.form.addEventListener('submit', saveMovie);
    document.addEventListener('click', event => {
      const uploadButton = event.target.closest('.upload-video-btn');
      if (uploadButton) uploadVideo(uploadButton);
      if (event.target.closest('.remove-episode-btn')) {
        event.target.closest('.admin-episode-row').remove();
      }
    });
    resetEditor();
  }

  init();
})();
