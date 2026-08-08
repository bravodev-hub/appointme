// Injected at image build time (Dockerfile ARG APP_VERSION → VITE_APP_VERSION);
// equals the git short SHA and the container image tag of the deployed build.
export const APP_VERSION: string = import.meta.env.VITE_APP_VERSION ?? 'dev';
