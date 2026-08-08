import js from '@eslint/js';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import globals from 'globals';
import tseslint from 'typescript-eslint';

export default tseslint.config(
    { ignores: ['dist'] },
    {
        extends: [js.configs.recommended, ...tseslint.configs.recommended],
        files: ['**/*.{ts,tsx}'],
        languageOptions: {
            ecmaVersion: 2020,
            globals: globals.browser,
        },
        plugins: {
            'react-hooks': reactHooks,
            'react-refresh': reactRefresh,
        },
        rules: {
            ...reactHooks.configs.recommended.rules,
            'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],
        },
    },
    {
        // Vendored shadcn/ui components ship cva variants and hooks alongside
        // their components by upstream design. Keeping them unsplit makes
        // future shadcn updates diff cleanly; the trade-off is full-reload
        // HMR when editing these files.
        files: ['src/components/ui/**/*.tsx'],
        rules: {
            'react-refresh/only-export-components': 'off',
        },
    },
);
