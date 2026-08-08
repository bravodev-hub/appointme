import { Empty, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui';
import { LogInIcon } from 'lucide-react';
import { useEffect } from 'react';
import { useSearchParams } from 'react-router';

export const Login = () => {
    const [params] = useSearchParams();
    useEffect(() => {
        const returnUrl = params.get('returnUrl') ?? '/';
        const provider = params.get('provider');

        const query = new URLSearchParams();
        query.set('returnUrl', returnUrl);
        if (provider) {
            query.set('provider', provider);
        }

        window.location.href = `/api/v1/login?${query.toString()}`;
    }, [params]);

    return (
        <Empty>
            <EmptyHeader>
                <EmptyMedia variant='icon'>
                    <LogInIcon />
                </EmptyMedia>
                <EmptyTitle>Redirecting to login page...</EmptyTitle>
            </EmptyHeader>
        </Empty>
    );
};
