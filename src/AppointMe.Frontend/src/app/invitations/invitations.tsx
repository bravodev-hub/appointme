import { useAcceptInvitation, useGetPendingInvitations } from '@/api/appointme';
import { ErrorWidget } from '@/components/error/error-widget';
import { Badge, Button, Spinner } from '@/components/ui';
import { Card, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { useQueryClient } from '@tanstack/react-query';
import { GalleryVerticalEnd } from 'lucide-react';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { toast } from 'sonner';

export const Invitations = () => {
    const navigate = useNavigate();
    const queryClient = useQueryClient();
    const { data, isLoading, isError, refetch } = useGetPendingInvitations();
    const { mutateAsync: acceptInvitation } = useAcceptInvitation();
    const [acceptingId, setAcceptingId] = useState<string | null>(null);

    if (isLoading) {
        return (
            <div className='flex min-h-svh items-center justify-center'>
                <Spinner className='size-8' />
            </div>
        );
    }

    if (isError) {
        return (
            <div className='flex min-h-svh items-center justify-center p-6'>
                <ErrorWidget title='Failed to load invitations' onRetry={() => refetch()} />
            </div>
        );
    }

    const invitations = data?.invitations ?? [];

    if (invitations.length === 0) {
        navigate('/', { replace: true });
        return null;
    }

    const handleAccept = async (id: string) => {
        setAcceptingId(id);
        try {
            await acceptInvitation({ id });
            toast.success('Invitation accepted');
            await queryClient.invalidateQueries();
            navigate('/', { replace: true });
        } catch {
            toast.error('Failed to accept invitation. Please try again.');
        } finally {
            setAcceptingId(null);
        }
    };

    return (
        <div className='bg-muted flex min-h-svh flex-col items-center justify-center gap-6 p-6 md:p-10'>
            <div className='flex w-full max-w-md flex-col gap-6'>
                <a href='#' className='flex items-center gap-2 self-center font-medium'>
                    <div className='bg-primary text-primary-foreground flex size-6 items-center justify-center rounded-md'>
                        <GalleryVerticalEnd className='size-4' />
                    </div>
                    <span className='font-extralight'>
                        Appoint<span className='font-semibold'>Me</span>
                    </span>
                </a>
                <h1 className='text-center text-2xl font-bold'>Pending Invitations</h1>
                <p className='text-muted-foreground text-center text-sm'>
                    You have been invited to join the following companies.
                </p>
                <div className='flex flex-col gap-4'>
                    {invitations.map(invitation => (
                        <Card key={invitation.id}>
                            <CardHeader>
                                <CardTitle>{invitation.companyName}</CardTitle>
                                <CardDescription>
                                    <div className='flex gap-1'>
                                        {invitation.roles.map(role => (
                                            <Badge key={role} variant='secondary'>
                                                {role}
                                            </Badge>
                                        ))}
                                    </div>
                                </CardDescription>
                            </CardHeader>
                            <CardFooter>
                                <Button
                                    className='w-full'
                                    onClick={() => handleAccept(invitation.id)}
                                    disabled={acceptingId !== null}
                                >
                                    {acceptingId === invitation.id ? (
                                        <>
                                            <Spinner />
                                            Accepting…
                                        </>
                                    ) : (
                                        'Accept invitation'
                                    )}
                                </Button>
                            </CardFooter>
                        </Card>
                    ))}
                </div>
            </div>
        </div>
    );
};
