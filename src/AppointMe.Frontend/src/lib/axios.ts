import { currentCompanyStore } from '@/components/auth/current-company-store';
import axios, { AxiosRequestConfig, AxiosResponse } from 'axios';

export const axiosInstance = axios.create({
    baseURL: '/',
    withCredentials: true,
    xsrfCookieName: 'XSRF-TOKEN',
    xsrfHeaderName: 'X-XSRF-TOKEN',
    withXSRFToken: true,
});

axiosInstance.interceptors.response.use(
    response => response,
    error => {
        if (error?.response?.status === 401) {
            if (!globalThis.location.pathname.startsWith('/auth/login')) {
                globalThis.location.href = '/auth/login';
            }
        }
        return Promise.reject(error);
    },
);

export const apiClient = async <T = unknown>(config: AxiosRequestConfig): Promise<T> => {
    const companyId = currentCompanyStore.get();
    const headers = {
        ...config.headers,
        ...(companyId ? { 'X-Company-Id': companyId } : {}),
    };

    const response: AxiosResponse<T> = await axiosInstance.request<T>({ ...config, headers });
    return response.data;
};
