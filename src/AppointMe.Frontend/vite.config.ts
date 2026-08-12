import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import path from 'path';
import { defineConfig, loadEnv } from 'vite';
import mkcert from 'vite-plugin-mkcert';
import svgr from 'vite-plugin-svgr';

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
    const env = loadEnv(mode, process.cwd(), '');
    const proxyTarget =
        env['services__appointme-api__https__0'] ||
        env['services__appointme-api__http__0'] ||
        env.VITE_API_PROXY_TARGET;

    return {
        plugins: [svgr(), react(), tailwindcss(), mkcert()],
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
