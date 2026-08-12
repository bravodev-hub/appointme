import { EditCustomerDialog } from './edit-customer-dialog';
import { useGetCustomer } from '@/api/appointme';
import { Can } from '@/components/auth/can';
import { FormattedDate } from '@/components/format';
import { useModalDialog } from '@/components/modal-dialog';
import { Avatar, AvatarFallback, Button, Skeleton } from '@/components/ui';
import { isAxiosError } from 'axios';
import { ReactNode } from 'react';
import { Navigate, useParams } from 'react-router';

const Field = ({ label, value }: { label: string; value: ReactNode }) => (
    <div>
        <dt className='text-muted-foreground text-sm'>{label}</dt>
        <dd className='mt-0.5 text-sm'>{value ?? <span className='text-muted-foreground'>—</span>}</dd>
    </div>
);

export const CustomerProfile = () => {
    const { id } = useParams<{ id: string }>();
    const modalDialog = useModalDialog();
    const { data: customer, isLoading, error } = useGetCustomer(id!, {
        query: {
            enabled: !!id,
        },
    });

    if (isAxiosError(error) && error.response?.status === 404) {
        return <Navigate to='/not-found' replace />;
    }

    if (isLoading) {
        return (
            <div className='mx-auto w-full max-w-2xl space-y-6'>
                <div className='flex items-center gap-4'>
                    <Skeleton className='size-16 rounded-full' />
                    <Skeleton className='h-7 w-48' />
                </div>
                <div className='grid grid-cols-2 gap-6'>
                    {Array.from({ length: 4 }).map((_, i) => (
                        <div key={i} className='space-y-1'>
                            <Skeleton className='h-4 w-20' />
                            <Skeleton className='h-4 w-32' />
                        </div>
                    ))}
                </div>
            </div>
        );
    }

    if (!customer) {
        return null;
    }

    return (
        <div className='mx-auto w-full max-w-2xl space-y-6'>
            <div className='flex items-center gap-4'>
                <Avatar className='size-16 text-lg'>
                    <AvatarFallback>{customer.initials}</AvatarFallback>
                </Avatar>
                <h1 className='text-2xl font-semibold tracking-tight'>{customer.fullName}</h1>
                <Can permission='customers:update'>
                    <Button
                        variant='outline'
                        size='sm'
                        onClick={() =>
                            modalDialog.open(props => <EditCustomerDialog {...props} customerId={customer.id} />)
                        }
                    >
                        Edit
                    </Button>
                </Can>
            </div>

            <dl className='grid grid-cols-2 gap-6'>
                <Field label='Email' value={customer.email} />
                <Field
                    label='Date of birth'
                    value={customer.dateOfBirth ? <FormattedDate date={customer.dateOfBirth} format='dayMonthShortYear' /> : null}
                />
                <Field label='Gender' value={customer.gender} />
                <Field
                    label='Customer since'
                    value={<FormattedDate date={customer.registrationDate} format='dayMonthShortYear' />}
                />
            </dl>
        </div>
    );
};
