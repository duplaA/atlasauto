// docs/js/script.js
// === SUPABASE CONFIG ===
const SUPABASE_URL = 'https://rikenjfgogyhdhjcasse.supabase.co';
const SUPABASE_ANON_KEY = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InJpa2VuamZnb2d5aGRoamNhc3NlIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NjMyMDU0MTcsImV4cCI6MjA3ODc4MTQxN30.bijOb9mbVA1sXePkRI7mRHMuv1GR8v_Bj0HTBab8Thw'; // ← Paste here

const { createClient } = supabase;
const _supabase = createClient(SUPABASE_URL, SUPABASE_ANON_KEY);

document.addEventListener('DOMContentLoaded', async () => {
    const displayNameInput = document.getElementById('display-name');
    const postContent = document.getElementById('post-content');
    const submitButton = document.getElementById('submit-post');
    const postsContainer = document.getElementById('posts-container');

    // Session name
    if (sessionStorage.getItem('displayName')) {
        displayNameInput.value = sessionStorage.getItem('displayName');
    }
    displayNameInput.addEventListener('input', () => {
        sessionStorage.setItem('displayName', displayNameInput.value);
    });

    // Load posts from Supabase
    async function loadPosts() {
        const { data: posts, error } = await _supabase
            .from('posts')
            .select('*')
            .order('timestamp', { ascending: false });

        if (error) {
            console.error('Error loading posts:', error);
            postsContainer.innerHTML = '<p>Failed to load posts.</p>';
            return;
        }

        postsContainer.innerHTML = '';
        posts.forEach(post => {
            const postEl = document.createElement('div');
            postEl.className = 'post';

            const header = document.createElement('div');
            header.className = 'post-header';

            const name = document.createElement('span');
            name.className = 'post-name';
            name.textContent = post.name;

            const time = document.createElement('span');
            time.className = 'post-time';
            time.textContent = new Date(post.timestamp).toLocaleString();

            // Delete button (only for your posts)
            const deleteBtn = document.createElement('button');
            deleteBtn.className = 'delete-btn';
            deleteBtn.textContent = 'Delete';
            deleteBtn.onclick = async () => {
                if (confirm('Delete this post?')) {
                    await _supabase.from('posts').delete().eq('id', post.id);
                    loadPosts();
                }
            };

            header.appendChild(name);
            header.appendChild(time);
            if (post.name === displayNameInput.value.trim()) {
                header.appendChild(deleteBtn);
            }

            const content = document.createElement('div');
            content.className = 'post-content';
            content.innerHTML = linkify(post.content);

            postEl.appendChild(header);
            postEl.appendChild(content);
            postsContainer.appendChild(postEl);
        });
    }

    // Linkify URLs
    function linkify(text) {
        const urlRegex = /(https?:\/\/[^\s]+)/g;
        return text.replace(urlRegex, url =>
            `<a href="${url}" target="_blank" rel="noopener noreferrer">${url}</a>`
        );
    }

    // Submit post
    submitButton.addEventListener('click', async () => {
        const name = displayNameInput.value.trim();
        const content = postContent.value.trim();

        if (!name || !content) {
            alert('Name and content required!');
            return;
        }

        const { error } = await _supabase
            .from('posts')
            .insert({ name, content });

        if (error) {
            alert('Failed to post: ' + error.message);
        } else {
            postContent.value = '';
            loadPosts();
        }
    });

    _supabase
        .channel('posts')
        .on('postgres_changes', { event: 'INSERT', schema: 'public', table: 'posts' }, () => loadPosts())
        .on('postgres_changes', { event: 'DELETE', schema: 'public', table: 'posts' }, () => loadPosts())
        .subscribe((status) => {
            console.log('Supabase real-time status:', status);
        });

    loadPosts();
});