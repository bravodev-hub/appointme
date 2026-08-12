import { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyTitle } from '@/components/ui';
import { Link } from 'react-router';

export const NotFound = () => {
    return (
        <div className='flex min-h-screen items-center justify-center p-6'>
            <Empty>
                <EmptyHeader>
                    <EmptyTitle>404 - Not Found</EmptyTitle>
                    <EmptyDescription>The page you&apos;re looking for doesn&apos;t exist.</EmptyDescription>
                </EmptyHeader>
                <EmptyContent>
                    <EmptyDescription>
                        Need help? <Link to='/'>Contact support</Link>
                    </EmptyDescription>
                </EmptyContent>
            </Empty>
        </div>
    );
};
