// === CONFIG ===
const SUPABASE_URL = 'https://rikenjfgogyhdhjcasse.supabase.co';
const SUPABASE_ANON_KEY = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InJpa2VuamZnb2d5aGRoamNhc3NlIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NjMyMDU0MTcsImV4cCI6MjA3ODc4MTQxN30.bijOb9mbVA1sXePkRI7mRHMuv1GR8v_Bj0HTBab8Thw';
const PASSWORD = 'duszaverseny2025';

const { createClient } = supabase;
const _supabase = createClient(SUPABASE_URL, SUPABASE_ANON_KEY);

function getUserId() {
    let id = localStorage.getItem('atlas_user_id');
    if (!id) {
        id = crypto.randomUUID();
        localStorage.setItem('atlas_user_id', id);
    }
    return id;
}
const USER_ID = getUserId();

document.addEventListener('DOMContentLoaded', () => {
    // Elements
    const fab = document.getElementById('fab');
    const composerModal = document.getElementById('composer-modal');
    const passwordModal = document.getElementById('password-modal');
    const closeComposer = document.getElementById('close-composer');
    const cancelPassword = document.getElementById('cancel-password');
    const confirmPassword = document.getElementById('confirm-password');
    const passwordInput = document.getElementById('password-input');
    const displayNameInput = document.getElementById('display-name');
    const postContent = document.getElementById('post-content');
    const submitPost = document.getElementById('submit-post');
    const postsContainer = document.getElementById('posts-container');

    let pendingPost = null;

    // Autofill name
    const savedName = localStorage.getItem('atlas_display_name');
    if (savedName) displayNameInput.value = savedName;
    displayNameInput.addEventListener('input', () => {
        localStorage.setItem('atlas_display_name', displayNameInput.value);
    });

    // Open composer
    fab.addEventListener('click', () => composerModal.classList.remove('hidden'));
    closeComposer.addEventListener('click', () => composerModal.classList.add('hidden'));
    cancelPassword.addEventListener('click', () => {
        passwordModal.classList.add('hidden');
        pendingPost = null;
    });

    // Close modals on backdrop click
    [composerModal, passwordModal].forEach(modal => {
        modal.addEventListener('click', (e) => {
            if (e.target === modal) modal.classList.add('hidden');
        });
    });

    // Submit → show password modal
    submitPost.addEventListener('click', () => {
        const name = displayNameInput.value.trim();
        const content = postContent.value.trim();
        if (!name || !content) return alert('Name and content required');

        pendingPost = { name, content };
        composerModal.classList.add('hidden');
        passwordModal.classList.remove('hidden');
        setTimeout(() => passwordInput.focus(), 100);
    });

    // Confirm password
    confirmPassword.addEventListener('click', async () => {
        if (!pendingPost) return;
        const input = passwordInput.value;
        if (input !== PASSWORD) {
            alert('Incorrect password');
            return;
        }

        const { error } = await _supabase
            .from('posts')
            .insert({ name: pendingPost.name, content: pendingPost.content });

        if (error) {
            alert('Post failed: ' + error.message);
        } else {
            postContent.value = '';
            passwordModal.classList.add('hidden');
            passwordInput.value = '';
            pendingPost = null;
            loadPosts();
        }
    });

    // Enter key in password
    passwordInput.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') confirmPassword.click();
    });

    // Load posts
    async function loadPosts() {
        const { data: posts, error } = await _supabase
            .from('posts')
            .select('*')
            .order('timestamp', { ascending: false });

        if (error) { console.error(error); return; }

        postsContainer.innerHTML = '';
        posts.forEach((post, i) => {
            const postEl = document.createElement('div');
            postEl.className = 'post';
            postEl.style.animationDelay = `${i * 0.05}s`;

            // Avatar
            const avatar = document.createElement('div');
            avatar.className = 'avatar';
            avatar.textContent = post.name[0].toUpperCase();

            // Header
            const header = document.createElement('div');
            header.className = 'post-header';
            header.appendChild(avatar);
            const nameSpan = document.createElement('span');
            nameSpan.className = 'post-name';
            nameSpan.textContent = post.name;
            const timeSpan = document.createElement('span');
            timeSpan.className = 'post-time';
            timeSpan.textContent = new Date(post.timestamp).toLocaleString();
            header.appendChild(nameSpan);
            header.appendChild(timeSpan);

            // Content
            const content = document.createElement('div');
            content.className = 'post-content';
            content.innerHTML = linkify(post.content);

            // Actions
            const actions = document.createElement('div');
            actions.className = 'post-actions';

            const likeBtn = document.createElement('button');
            likeBtn.className = 'like-btn';
            const liked = post.likes?.includes(USER_ID);
            likeBtn.innerHTML = `
        <svg class="heart" width="18" height="18" viewBox="0 0 24 24" fill="${liked ? '#f91880' : 'none'}" stroke="currentColor" stroke-width="2">
          <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/>
        </svg>
        <span>${post.likes?.length || 0}</span>
      `;
            if (liked) likeBtn.classList.add('liked');

            likeBtn.onclick = async (e) => {
                e.stopPropagation(); // Critical
                const liked = post.likes?.includes(USER_ID);
                const newLikes = liked
                    ? post.likes.filter(id => id !== USER_ID)
                    : [...(post.likes || []), USER_ID];

                const { error } = await _supabase
                    .from('posts')
                    .update({ likes: newLikes })
                    .eq('id', post.id);

                if (!error) loadPosts();
            };

            actions.appendChild(likeBtn);
            postEl.appendChild(header);
            postEl.appendChild(content);
            postEl.appendChild(actions);
            postsContainer.appendChild(postEl);
        });
    }

    function linkify(text) {
        const urlRegex = /(https?:\/\/[^\s]+)/g;
        return text.replace(urlRegex, url =>
            `<a href="${url}" target="_blank" rel="noopener noreferrer" onclick="event.stopPropagation();" style="pointer-events: auto;">${url}</a>`
        );
    }

    // Real-time
    _supabase
        .channel('posts')
        .on('postgres_changes', { event: '*', schema: 'public', table: 'posts' }, () => loadPosts())
        .subscribe();

    loadPosts();
});