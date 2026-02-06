// ===== THEME SWITCHER WITH DARK/LIGHT/SYSTEM MODES =====

class ThemeSwitcher {
    constructor() {
        this.THEME_KEY = 'leadforge-theme';
        this.THEMES = {
            DARK: 'dark',
            LIGHT: 'light',
            SYSTEM: 'system'
        };

        this.init();
    }

    init() {
        // Load saved theme or default to dark
        const savedTheme = localStorage.getItem(this.THEME_KEY) || this.THEMES.DARK;
        this.applyTheme(savedTheme);

        // Listen for system theme changes
        this.setupSystemThemeListener();

        // Setup theme toggle buttons
        this.setupThemeToggles();
    }

    setupSystemThemeListener() {
        const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
        mediaQuery.addEventListener('change', (e) => {
            const currentTheme = localStorage.getItem(this.THEME_KEY);
            if (currentTheme === this.THEMES.SYSTEM) {
                this.applyTheme(this.THEMES.SYSTEM);
            }
        });
    }

    setupThemeToggles() {
        // Create theme toggle UI if it doesn't exist
        this.createThemeToggle();

        // Listen for theme changes
        document.addEventListener('click', (e) => {
            if (e.target.closest('[data-theme-toggle]')) {
                const theme = e.target.closest('[data-theme-toggle]').dataset.themeToggle;
                this.setTheme(theme);
            }
        });
    }

    createThemeToggle() {
        // Check if theme toggle already exists
        if (document.getElementById('theme-switcher')) return;

        const themeToggle = document.createElement('div');
        themeToggle.id = 'theme-switcher';
        themeToggle.className = 'theme-switcher';
        themeToggle.innerHTML = `
            <button class="theme-toggle-btn" id="theme-toggle-button" aria-label="Toggle theme">
                <i class="bi bi-moon-stars-fill theme-icon"></i>
            </button>
            <div class="theme-dropdown" id="theme-dropdown">
                <button class="theme-option" data-theme-toggle="${this.THEMES.DARK}">
                    <i class="bi bi-moon-stars-fill"></i>
                    <span>Dark</span>
                    <i class="bi bi-check-lg theme-check"></i>
                </button>
                <button class="theme-option" data-theme-toggle="${this.THEMES.LIGHT}">
                    <i class="bi bi-sun-fill"></i>
                    <span>Light</span>
                    <i class="bi bi-check-lg theme-check"></i>
                </button>
                <button class="theme-option" data-theme-toggle="${this.THEMES.SYSTEM}">
                    <i class="bi bi-laptop"></i>
                    <span>System</span>
                    <i class="bi bi-check-lg theme-check"></i>
                </button>
            </div>
        `;

        // Add to navbar or body
        const navbar = document.querySelector('.navbar .container-fluid, .nav-wrapper');
        if (navbar) {
            navbar.appendChild(themeToggle);
        } else {
            document.body.appendChild(themeToggle);
        }

        // Toggle dropdown
        const toggleBtn = document.getElementById('theme-toggle-button');
        const dropdown = document.getElementById('theme-dropdown');

        toggleBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            dropdown.classList.toggle('show');
        });

        // Close dropdown when clicking outside
        document.addEventListener('click', () => {
            dropdown.classList.remove('show');
        });
    }

    setTheme(theme) {
        localStorage.setItem(this.THEME_KEY, theme);
        this.applyTheme(theme);
        this.updateThemeIcon(theme);
        this.updateActiveThemeOption(theme);
    }

    applyTheme(theme) {
        const root = document.documentElement;
        const body = document.body;

        // Remove all theme classes
        body.classList.remove('theme-dark', 'theme-light', 'theme-system');

        let effectiveTheme = theme;

        // Handle system theme
        if (theme === this.THEMES.SYSTEM) {
            const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
            effectiveTheme = prefersDark ? this.THEMES.DARK : this.THEMES.LIGHT;
            body.classList.add('theme-system');
        }

        // Apply theme
        body.classList.add(`theme-${effectiveTheme}`);
        root.setAttribute('data-theme', effectiveTheme);

        // Update meta theme-color for mobile browsers
        this.updateMetaThemeColor(effectiveTheme);

        // Update icon
        this.updateThemeIcon(theme);
        this.updateActiveThemeOption(theme);
    }

    updateMetaThemeColor(theme) {
        let metaThemeColor = document.querySelector('meta[name="theme-color"]');
        if (!metaThemeColor) {
            metaThemeColor = document.createElement('meta');
            metaThemeColor.name = 'theme-color';
            document.head.appendChild(metaThemeColor);
        }

        metaThemeColor.content = theme === this.THEMES.LIGHT ? '#ffffff' : '#0f172a';
    }

    updateThemeIcon(theme) {
        const icon = document.querySelector('#theme-toggle-button .theme-icon');
        if (!icon) return;

        icon.className = 'bi theme-icon';

        if (theme === this.THEMES.LIGHT) {
            icon.classList.add('bi-sun-fill');
        } else if (theme === this.THEMES.DARK) {
            icon.classList.add('bi-moon-stars-fill');
        } else {
            icon.classList.add('bi-laptop');
        }
    }

    updateActiveThemeOption(theme) {
        const options = document.querySelectorAll('.theme-option');
        options.forEach(option => {
            if (option.dataset.themeToggle === theme) {
                option.classList.add('active');
            } else {
                option.classList.remove('active');
            }
        });
    }

    getCurrentTheme() {
        return localStorage.getItem(this.THEME_KEY) || this.THEMES.DARK;
    }
}

// Initialize theme switcher when DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        window.themeSwitcher = new ThemeSwitcher();
    });
} else {
    window.themeSwitcher = new ThemeSwitcher();
}

// Export for use in other scripts
if (typeof module !== 'undefined' && module.exports) {
    module.exports = ThemeSwitcher;
}
