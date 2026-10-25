import { defineConfig } from 'orval';

export default defineConfig({
    appointme: {
        input: {
            target: '../AppointMe.Api/openapi.json',
        },
        output: {
            target: './src/api/client.ts',
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
            afterAllFilesWrite: 'vp fmt',
        },
    },
});
