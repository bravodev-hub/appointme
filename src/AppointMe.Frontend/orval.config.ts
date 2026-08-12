import { defineConfig } from 'orval';

export default defineConfig({
    appointme: {
        input: {
            target: 'https://localhost:7233/openapi/v1.json',
        },
        output: {
            target: './src/api/appointme.ts',
            mode: 'split',
            httpClient: 'axios',
            client: 'react-query',
            override: {
                query: {
                    useSuspenseQuery: true,
                },
                mutator: {
                    path: 'src/lib/axios.ts',
                    name: 'apiClient',
                },
            },
            clean: true,
        },
        hooks: {
            afterAllFilesWrite: 'prettier --write',
        },
    },
});
