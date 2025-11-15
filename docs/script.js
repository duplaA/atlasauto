// === CONFIG ===
const SUPABASE_URL = 'https://rikenjfgogyhdhjcasse.supabase.co';
const SUPABASE_ANON_KEY = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InJpa2VuamZnb2d5aGRoamNhc3NlIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NjMyMDU0MTcsImV4cCI6MjA3ODc4MTQxN30.bijOb9mbVA1sXePkRI7mRHMuv1GR8v_Bj0HTBab8Thw';
const PASSWORD = 'duszaverseny2025';

const { createClient } = supabase;
const _supabase = createClient(SUPABASE_URL, SUPABASE_ANON_KEY);

// === GITHUB API (DIRECT — NO PROXY) ===
const GITHUB_API = 'https://api.github.com/repos/duplaa/atlasauto';
const GITHUB_HEADERS = {
    'Accept': 'application/vnd.github.v3+json',
    'User-Agent': 'AtlasAuto/1.0 (+https://duplaa.github.io/atlasauto)'
};

// === CACHE (2 min TTL) ===
const cache = {
    issues: { data: null, ts: 0 },
    commits24h: { data: null, ts: 0 },
    latestCommit: { data: null, ts: 0 },
    latestRelease: { data: null, ts: 0 }
};
const CACHE_TTL = 2 * 60 * 1000; // 2 minutes

// === USER ID ===
function getUserId() {
    let id = localStorage.getItem('atlas_user_id');
    if (!id) { id = crypto.randomUUID(); localStorage.setItem('atlas_user_id', id); }
    return id;
}
const USER_ID = getUserId();

// === SVG ICONS ===
const ICONS = {
    home: `<svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M3 11.5L12 4l9 7.5V20a1 1 0 0 1-1 1h-5v-6H9v6H4a1 1 0 0 1-1-1v-8.5z" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"/></svg>`,
    plus: `<svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M12 5v14M5 12h14" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg>`,
    issue: `<svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M12 2a10 10 0 1 0 .001 20.001A10 10 0 0 0 12 2zm0 6v6" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"/></svg>`,
    github: `<svg width="20" height="20" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true" xmlns="http://www.w3.org/2000/svg">
    <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38
    0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13
    -.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07
    -1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12
    0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27s1.36.09 2 .27c1.53-1.04 2.2-.82 2.2-.82
    .44 1.1.16 1.92.08 2.12.51.56.82 1.28.82 2.15 0 3.07-1.87 3.75-3.65 3.95
    .29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0 0 16 8
    c0-4.42-3.58-8-8-8z"/>
  </svg>`
};

// === UTILITIES ===
function escapeHtml(s = '') {
    return String(s).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
}

function linkify(text) {
    if (!text) return '';
    const escaped = escapeHtml(text);
    const urlRegex = /(https?:\/\/[^\s]+)/g;
    return escaped.replace(urlRegex, rawUrl => {
        const url = rawUrl.trim();
        return `<a href="${url}" target="_blank" rel="noopener noreferrer" onclick="event.stopPropagation();">${url}</a>`;
    });
}

// === GITHUB FETCH (DIRECT + CACHE + RATE LIMIT GRACEFUL) ===
async function githubFetch(endpoint, cacheKey) {
    const now = Date.now();
    if (cache[cacheKey]?.ts > now - CACHE_TTL && cache[cacheKey].data !== null) {
        return cache[cacheKey].data;
    }

    const url = `${GITHUB_API}${endpoint}`;
    try {
        const res = await fetch(url, { headers: GITHUB_HEADERS });
        if (!res.ok) {
            if (res.status === 403 || res.status === 429) {
                console.warn('GitHub rate limit hit. Using cache.');
                return cache[cacheKey]?.data ?? [];
            }
            throw new Error(`HTTP ${res.status}`);
        }
        const data = await res.json();
        cache[cacheKey] = { data, ts: now };
        return data;
    } catch (err) {
        console.warn(`GitHub fetch failed (${endpoint}):`, err.message);
        return cache[cacheKey]?.data ?? [];
    }
}

// === DOM READY ===
document.addEventListener('DOMContentLoaded', () => {
    const postsContainer = document.getElementById('posts-container');
    const composerModal = document.getElementById('composer-modal');
    const passwordModal = document.getElementById('password-modal');
    const closeComposer = document.getElementById('close-composer');
    const cancelPassword = document.getElementById('cancel-password');
    const confirmPassword = document.getElementById('confirm-password');
    const passwordInput = document.getElementById('password-input');
    const postContent = document.getElementById('post-content');
    const submitPost = document.getElementById('submit-post');
    const avatarPreview = document.getElementById('avatar-preview');
    const namePreview = document.getElementById('name-preview');
    const charCounter = document.getElementById('char-counter');
    const titleContainer = document.getElementById('title-container');
    const bottomNav = document.getElementById('bottom-nav');
    const appBody = document.querySelector('.app-body');

    const oldFab = document.getElementById('fab');
    if (oldFab) oldFab.remove();

    const MAX_CHARS = 280;
    let pendingPost = null;
    let cachedFeed = [];

    // === BOTTOM NAV ICONS ===
    if (bottomNav) {
        const map = { 'tab-home': ICONS.home, 'tab-new': ICONS.plus, 'tab-issues': ICONS.issue, 'tab-github': ICONS.github };
        bottomNav.querySelectorAll('button').forEach(btn => {
            const iconSpan = btn.querySelector('.icon');
            if (iconSpan && map[btn.id]) iconSpan.innerHTML = map[btn.id];
        });
    }

    // === NAME & AVATAR ===
    const savedName = localStorage.getItem('atlas_display_name') || '';
    namePreview.textContent = savedName || '';
    updatePreview();

    function updatePreview() {
        const name = namePreview.textContent.trim();
        const firstLetter = name[0]?.toUpperCase() || 'S';
        avatarPreview.textContent = firstLetter;
    }
    namePreview.addEventListener('input', () => {
        const name = namePreview.textContent.trim();
        localStorage.setItem('atlas_display_name', name);
        updatePreview();
        validatePost();
    });
    namePreview.addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); namePreview.blur(); } });

    // === VALIDATION ===
    function validatePost() {
        const len = (postContent.value || '').length;
        const hasContent = len > 0 && len <= MAX_CHARS;
        const hasName = (namePreview.textContent || '').trim().length > 0;
        charCounter.textContent = MAX_CHARS - len;
        charCounter.classList.toggle('warning', len > MAX_CHARS * 0.9);
        submitPost.disabled = !(hasContent && hasName);
    }
    postContent.addEventListener('input', validatePost);
    validatePost();

    // === MODALS ===
    function openComposer() {
        composerModal.classList.remove('hidden');
        setTimeout(() => postContent.focus(), 100);
    }
    closeComposer?.addEventListener('click', () => {
        composerModal.classList.add('hidden');
        postContent.value = '';
        validatePost();
    });
    cancelPassword?.addEventListener('click', () => {
        passwordModal.classList.add('hidden');
        pendingPost = null;
    });
    [composerModal, passwordModal].forEach(modal => {
        modal?.addEventListener('click', e => {
            if (e.target === modal) {
                modal.classList.add('hidden');
                if (modal === composerModal) { postContent.value = ''; validatePost(); }
            }
        });
    });

    // === GITHUB ISSUES ===
    async function loadGitHubData() {
        try {
            // remove previous system posts
            document.querySelectorAll('.post.system-issue').forEach(el => el.remove());
            const issuesRes = await fetch(`${GITHUB_API}/issues?state=open&per_page=10`);
            const issues = await issuesRes.json();
            if (!Array.isArray(issues)) return;

            issues.forEach(issue => {
                const systemPost = document.createElement('div');
                systemPost.className = 'post system-issue';

                const header = document.createElement('div');
                header.className = 'post-header';

                const avatar = document.createElement('div');
                avatar.className = 'avatar';
                // try to use user's avatar from GitHub
                if (issue.user && issue.user.avatar_url) {
                    const img = document.createElement('img');
                    img.src = issue.user.avatar_url;
                    img.alt = issue.user.login;
                    img.style.width = '100%';
                    img.style.height = '100%';
                    img.style.objectFit = 'cover';
                    img.style.borderRadius = '50%';
                    avatar.appendChild(img);
                } else {
                    avatar.textContent = 'S';
                }

                const nameSpan = document.createElement('span');
                nameSpan.className = 'post-name';
                nameSpan.textContent = 'System';

                const timeSpan = document.createElement('span');
                timeSpan.className = 'post-time';
                timeSpan.textContent = new Date(issue.created_at).toLocaleString('hu-HU', {
                    month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit'
                });

                header.appendChild(avatar);
                header.appendChild(nameSpan);
                header.appendChild(timeSpan);

                const content = document.createElement('div');
                content.className = 'post-content';

                const title = (issue.title || '').trim();
                const author = issue.user?.login ? issue.user.login.trim() : 'unknown';
                // small and link as separate elements (no leading spaces)
                content.innerHTML = `
                    <div><strong>Issue #${issue.number}</strong>: ${escapeHtml(title)}</div>
                    <small>by @${escapeHtml(author)}</small>
                    <a href="${issue.html_url}" target="_blank" class="issue-link" onclick="event.stopPropagation();">View on GitHub</a>
                `;

                systemPost.appendChild(header);
                systemPost.appendChild(content);

                // append to the top of posts container (so issues are visible)
                postsContainer.insertBefore(systemPost, postsContainer.firstChild);
            });
        } catch (err) { console.error('GitHub API error:', err); }
    }

    // === SUBMIT POST ===
    submitPost?.addEventListener('click', () => {
        const name = (namePreview.textContent || '').trim();
        const content = (postContent.value || '').trim();
        if (!name || !content || content.length > MAX_CHARS) {
            alert('Please enter a name and valid post (1–280 characters).');
            return;
        }
        pendingPost = { name, content };
        composerModal.classList.add('hidden');
        passwordModal.classList.remove('hidden');
        setTimeout(() => passwordInput.focus(), 100);
    });

    confirmPassword?.addEventListener('click', async () => {
        if (!pendingPost) return;
        if ((passwordInput.value || '') !== PASSWORD) { alert('Incorrect password'); return; }

        const { error } = await _supabase.from('posts').insert({
            name: pendingPost.name, content: pendingPost.content, likes: [], timestamp: new Date().toISOString()
        });

        if (error) { alert('Post failed: ' + error.message); }
        else {
            postContent.value = '';
            passwordModal.classList.add('hidden');
            passwordInput.value = '';
            pendingPost = null;
            validatePost();
            await loadFeed();
        }
    });

    passwordInput?.addEventListener('keydown', e => { if (e.key === 'Enter') confirmPassword.click(); });

    // === POST ELEMENTS ===
    function createUserPostElement(post) {
        const postEl = document.createElement('div');
        postEl.className = 'post';
        postEl.dataset.type = 'user';
        postEl.dataset.timestamp = post.timestamp || '';

        const header = document.createElement('div');
        header.className = 'post-header';

        const avatar = document.createElement('div');
        avatar.className = 'avatar';
        avatar.textContent = (post.name || 'S')[0]?.toUpperCase();

        const nameSpan = document.createElement('span');
        nameSpan.className = 'post-name';
        nameSpan.textContent = post.name || 'Unknown';

        const timeSpan = document.createElement('span');
        timeSpan.className = 'post-time';
        timeSpan.textContent = new Date(post.timestamp).toLocaleString('hu-HU', {
            month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit'
        });

        header.appendChild(avatar);
        header.appendChild(nameSpan);
        header.appendChild(timeSpan);

        const content = document.createElement('div');
        content.className = 'post-content';
        content.innerHTML = linkify((post.content || '').trim());

        const actions = document.createElement('div');
        actions.className = 'post-actions';

        const likeBtn = document.createElement('button');
        likeBtn.className = 'like-btn';
        const liked = Array.isArray(post.likes) && post.likes.includes(USER_ID);
        const likeCount = Array.isArray(post.likes) ? post.likes.length : 0;

        likeBtn.innerHTML = `
      <svg class="heart" width="18" height="18" viewBox="0 0 24 24" 
           fill="${liked ? '#f91880' : 'none'}" 
           stroke="${liked ? '#f91880' : 'currentColor'}" 
           stroke-width="2" style="background:transparent;">
        <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/>
      </svg>
      <span>${likeCount}</span>
    `;
        if (liked) likeBtn.classList.add('liked');
        likeBtn.onclick = async e => {
            e.stopPropagation();
            const currentLikes = Array.isArray(post.likes) ? post.likes : [];
            const isLiked = currentLikes.includes(USER_ID);
            const newLikes = isLiked ? currentLikes.filter(id => id !== USER_ID) : [...currentLikes, USER_ID];
            const { error } = await _supabase.from('posts').update({ likes: newLikes }).eq('id', post.id);
            if (!error) await loadFeed();
        };

        actions.appendChild(likeBtn);
        postEl.appendChild(header);
        postEl.appendChild(content);
        postEl.appendChild(actions);
        return postEl;
    }

    function createSystemIssueElement(issue) {
        const postEl = document.createElement('div');
        postEl.className = 'post system-issue';
        postEl.dataset.type = 'system';
        postEl.dataset.timestamp = issue.created_at || '';

        const header = document.createElement('div');
        header.className = 'post-header';

        const avatar = document.createElement('div');
        avatar.className = 'avatar';
        if (issue.user?.avatar_url) {
            const img = document.createElement('img');
            img.src = issue.user.avatar_url;
            img.alt = issue.user.login || 'avatar';
            img.style.cssText = 'width:100%;height:100%;object-fit:cover;border-radius:50%';
            avatar.appendChild(img);
        } else avatar.textContent = 'S';

        const nameSpan = document.createElement('span');
        nameSpan.className = 'post-name';
        nameSpan.textContent = 'System';

        const timeSpan = document.createElement('span');
        timeSpan.className = 'post-time';
        timeSpan.textContent = new Date(issue.created_at).toLocaleString('hu-HU', {
            month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit'
        });

        header.appendChild(avatar);
        header.appendChild(nameSpan);
        header.appendChild(timeSpan);

        const content = document.createElement('div');
        content.className = 'post-content';
        const title = (issue.title || '').trim();
        const author = issue.user?.login ? issue.user.login.trim() : 'unknown';
        content.innerHTML = `
      <div><strong>Issue #${issue.number}</strong>: ${escapeHtml(title)}</div>
      <small>by @${escapeHtml(author)}</small>
      <a href="${issue.html_url}" target="_blank" class="issue-link" onclick="event.stopPropagation();">View on GitHub</a>
    `;

        postEl.appendChild(header);
        postEl.appendChild(content);
        return postEl;
    }

    // === LOAD FEED ===
    async function loadFeed() {
        try {
            const { data: userPosts, error: postsError } = await _supabase.from('posts').select('*').order('timestamp', { ascending: false });
            if (postsError) { console.error('Load posts error:', postsError); return; }
            const posts = Array.isArray(userPosts) ? userPosts.map(p => ({ kind: 'user', data: p, ts: new Date(p.timestamp).getTime() })) : [];

            let issues = [];
            try {
                const issuesJson = await githubFetch('/issues?state=open&per_page=20', 'issues');
                if (Array.isArray(issuesJson)) {
                    issues = issuesJson.map(i => ({ kind: 'system', data: i, ts: new Date(i.created_at).getTime() }));
                }
            } catch (e) { console.error('GitHub issues fetch failed:', e); }

            const merged = [...posts, ...issues].sort((a, b) => b.ts - a.ts);
            cachedFeed = merged;

            postsContainer.innerHTML = '';
            merged.forEach((item, i) => {
                const el = item.kind === 'user' ? createUserPostElement(item.data) : createSystemIssueElement(item.data);
                el.style.animationDelay = `${i * 0.05}s`;
                postsContainer.appendChild(el);
            });
        } catch (err) {
            console.error('loadFeed error:', err);
        }
    }

    // === FILTERS ===
    function showAll() { renderFeed(cachedFeed); }
    function showIssuesOnly() { renderFeed(cachedFeed.filter(i => i.kind === 'system')); }
    function renderFeed(items) {
        if (!items.length) { loadFeed(); return; }
        postsContainer.innerHTML = '';
        items.forEach((item, i) => {
            const el = item.kind === 'user' ? createUserPostElement(item.data) : createSystemIssueElement(item.data);
            el.style.animationDelay = `${i * 0.05}s`;
            postsContainer.appendChild(el);
        });
    }

    // === GITHUB PAGE (MOBILE) ===
    function showGitHubPage() {
        document.body.classList.add('github-open');
        const mobilePage = document.getElementById('github-mobile');
        if (mobilePage) {
            mobilePage.classList.remove('hidden');
            mobilePage.setAttribute('aria-hidden', 'false');
        }
        loadGitHubSidebar(true);
    }
    function hideGitHubPage() {
        document.body.classList.remove('github-open');
        const mobilePage = document.getElementById('github-mobile');
        if (mobilePage) {
            mobilePage.classList.add('hidden');
            mobilePage.setAttribute('aria-hidden', 'true');
        }
    }

    // === GITHUB SIDEBAR + MOBILE STATS ===
    async function loadGitHubSidebar() {
        try {
            const since = new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString();
            const commitsRes = await fetch(`${GITHUB_API}/commits?since=${encodeURIComponent(since)}&per_page=100`);
            const commits = await commitsRes.json();
            const commitCountEl = document.getElementById('commit-count');
            if (commitCountEl) commitCountEl.textContent = Array.isArray(commits) ? String(commits.length) : '—';

            // latest commit
            const latestCommitRes = await fetch(`${GITHUB_API}/commits?per_page=1`);
            const latestCommits = await latestCommitRes.json();
            const latestCommitEl = document.getElementById('latest-commit');
            if (latestCommitEl) {
                if (Array.isArray(latestCommits) && latestCommits[0]) {
                    const sha = latestCommits[0].sha.slice(0, 7);
                    const message = (latestCommits[0].commit.message || '').split('\n')[0];
                    latestCommitEl.textContent = `${sha} — ${message}`;
                    latestCommitEl.style.cursor = 'pointer';
                    latestCommitEl.onclick = () => window.open(latestCommits[0].html_url, '_blank');
                } else {
                    latestCommitEl.textContent = '—';
                    latestCommitEl.onclick = null;
                }
            }

            // latest release
            const releaseRes = await fetch(`${GITHUB_API}/releases/latest`);
            const latestReleaseEl = document.getElementById('latest-release');
            if (releaseRes.ok && latestReleaseEl) {
                const release = await releaseRes.json();
                latestReleaseEl.textContent = `${release.name || release.tag_name} • ${new Date(release.published_at).toLocaleDateString('hu-HU')}`;
                latestReleaseEl.style.cursor = 'pointer';
                latestReleaseEl.onclick = () => window.open(release.html_url, '_blank');
            } else if (latestReleaseEl) {
                latestReleaseEl.textContent = '—';
                latestReleaseEl.onclick = null;
            }
        } catch (err) {
            console.error('GitHub sidebar error:', err);
            const commitCountEl = document.getElementById('commit-count');
            const latestCommitEl = document.getElementById('latest-commit');
            const latestReleaseEl = document.getElementById('latest-release');
            if (commitCountEl) commitCountEl.textContent = '—';
            if (latestCommitEl) latestCommitEl.textContent = '—';
            if (latestReleaseEl) latestReleaseEl.textContent = '—';
        }
    }

    // === REAL-TIME SUBSCRIBE ===
    try {
        _supabase.channel('posts')
            .on('postgres_changes', { event: '*', schema: 'public', table: 'posts' }, () => {
                loadPosts();
                setTimeout(loadGitHubData, 500);
            })
            .subscribe();
    } catch (err) {
        console.warn('Realtime not available:', err);
    }


    // === REAL-TIME ===
    try {
        _supabase.channel('posts')
            .on('postgres_changes', { event: '*', schema: 'public', table: 'posts' }, () => loadFeed())
            .subscribe();
    } catch (err) { console.warn('Realtime not available:', err); }

    // === PILL BOUNCE ===
    let lastScrollY = window.scrollY;
    window.addEventListener('scroll', () => {
        const current = window.scrollY;
        const delta = current - lastScrollY;
        if (Math.abs(delta) > 10) {
            if (delta > 0) { titleContainer.classList.remove('bounce-up'); titleContainer.classList.add('bounce-down'); }
            else { titleContainer.classList.remove('bounce-down'); titleContainer.classList.add('bounce-up'); }
            lastScrollY = current;
        }
    });

    // === APP MODE & NAV ===
    function isStandalone() {
        return (window.matchMedia && window.matchMedia('(display-mode: standalone)').matches) || window.navigator.standalone === true;
    }
    function applyAppMode() {
        const appShell = document.querySelector('.app-shell') || document.body;
        if (isStandalone()) {
            titleContainer?.classList.add('compact');
            appShell.classList.add('standalone');
        } else {
            titleContainer?.classList.remove('compact');
            appShell.classList.remove('standalone');
        }
    }
    applyAppMode();
    window.addEventListener('visibilitychange', applyAppMode);

    if (bottomNav) {
        bottomNav.querySelectorAll('button').forEach(btn => {
            btn.addEventListener('click', () => {
                bottomNav.querySelectorAll('button').forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                if (btn.id === 'tab-new') openComposer();
                else if (btn.id === 'tab-issues') showIssuesOnly();
                else if (btn.id === 'tab-github') showGitHubPage();
                else { showAll(); hideGitHubPage(); }
            });
        });
    }
    document.getElementById('github-mobile-back')?.addEventListener('click', () => {
        document.getElementById('tab-home')?.click();
        hideGitHubPage();
    });

    // === PULL TO REFRESH ===
    if (appBody) {
        let startY = 0;
        appBody.addEventListener('touchstart', e => startY = e.touches[0].clientY);
        appBody.addEventListener('touchmove', e => {
            const y = e.touches[0].clientY;
            if (appBody.scrollTop <= 2 && y - startY > 20) appBody.classList.add('pull-ready');
        });
        appBody.addEventListener('touchend', () => {
            if (appBody.classList.contains('pull-ready')) {
                loadFeed(); loadGitHubSidebar();
                setTimeout(() => appBody.classList.remove('pull-ready'), 800);
            }
        });
    }

    // === INITIAL LOAD ===
    loadFeed();
    loadGitHubSidebar(true);
    setInterval(() => loadGitHubSidebar(true), 3 * 60 * 1000);
});