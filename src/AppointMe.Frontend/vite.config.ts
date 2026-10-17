import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import path from 'path';
import mkcert from 'vite-plugin-mkcert';
import svgr from 'vite-plugin-svgr';
import { defineConfig, loadEnv, lazyPlugins } from 'vite-plus';

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
    const env = loadEnv(mode, process.cwd(), '');
    // One lookup per line: the template rename changes the length of these names, and a
    // combined expression would wrap or unwrap under Oxfmt depending on the project name.
    const aspireApiHttps = env['services__appointme-api__https__0'];
    const aspireApiHttp = env['services__appointme-api__http__0'];
    const proxyTarget = aspireApiHttps || aspireApiHttp || env.VITE_API_PROXY_TARGET;

    return {
        lint: {
            // Oxlint's defaults (oxc, typescript, unicorn) plus React, with every correctness rule failing CI.
            plugins: ['oxc', 'typescript', 'unicorn', 'react'],
            categories: { correctness: 'error' },
            ignorePatterns: ['dist'],
            rules: {
                'react/rules-of-hooks': 'error',
                'react/only-export-components': ['warn', { allowConstantExport: true }],
                // Kept from the ESLint / typescript-eslint recommended sets this config replaced.
                'typescript/no-explicit-any': 'error',
                'typescript/ban-ts-comment': 'error',
                'prefer-const': 'error',
                'no-var': 'error',
                'no-empty': 'error',
                'vite-plus/prefer-vite-plus-imports': 'error',
            },
            overrides: [
                {
                    // Vendored shadcn/ui components ship cva variants and hooks alongside their components by
                    // upstream design. Keeping them unsplit makes future shadcn updates diff cleanly; the
                    // trade-off is full-reload HMR when editing these files.
                    files: ['src/components/ui/**/*.tsx'],
                    rules: { 'react/only-export-components': 'off' },
                },
            ],
            jsPlugins: [{ name: 'vite-plus', specifier: 'vite-plus/oxlint-plugin' }],
            options: {
                typeAware: true,
                typeCheck: true,
            },
        },
        fmt: {
            printWidth: 120,
            tabWidth: 4,
            semi: true,
            singleQuote: true,
            jsxSingleQuote: true,
            arrowParens: 'avoid',
            useTabs: false,
            trailingComma: 'all',
            sortPackageJson: false,
            sortTailwindcss: {},
            sortImports: {
                // One merged group = plain alphabetical order by source, which is what
                // @trivago/prettier-plugin-sort-imports produced with no importOrder set.
                groups: [
                    [
                        'type-import',
                        'value-builtin',
                        'value-external',
                        'type-internal',
                        'value-internal',
                        'type-parent',
                        'type-sibling',
                        'type-index',
                        'value-parent',
                        'value-sibling',
                        'value-index',
                        'unknown',
                    ],
                ],
            },
            // Markdown (docs, vendored agent skills) and tool-written JSON stay unformatted.
            ignorePatterns: ['dist', '.agents/**', '.claude/**', '**/*.md', 'components.json', 'skills-lock.json'],
            overrides: [
                {
                    // orval's generated client: sorting would move its file-header comment (attached to the
                    // first import orval emits) down into the middle of the imports.
                    files: ['src/api/**'],
                    options: { sortImports: false },
                },
            ],
        },
        plugins: lazyPlugins(() => [svgr(), react(), tailwindcss(), mkcert()]),
        resolve: {
            alias: {
                '@': path.resolve(import.meta.dirname, './src'),
            },
        },
        server: {
            host: 'localhost',
            proxy: {
                '^/api': {
                    target: proxyTarget,
                    secure: false,
                    changeOrigin: true,
                },
                '^/signin-oidc': {
                    target: proxyTarget,
                    secure: false,
                    changeOrigin: true,
                },
                '^/admin': {
                    target: proxyTarget,
                    secure: false,
                    changeOrigin: true,
                },
            },
        },
    };
});
