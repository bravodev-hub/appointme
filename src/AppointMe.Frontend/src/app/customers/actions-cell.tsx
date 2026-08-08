import { EditCustomerDialog } from './edit-customer-dialog';
import { CustomerDto } from '@/api/appointme.schemas';
import { getGetCustomersQueryKey, useDeleteCustomer } from '@/api/appointme.ts';
import { Can } from '@/components/auth/can';
import { ConfirmDialog, useModalDialog } from '@/components/modal-dialog';
import {
    Button,
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuSeparator,
    DropdownMenuTrigger,
} from '@/components/ui';
import { useQueryClient } from '@tanstack/react-query';
import { EllipsisVerticalIcon } from 'lucide-react';
import { toast } from 'sonner';

export const ActionsCell = ({ row }: { row: { original: CustomerDto } }) => {
    const modalDialog = useModalDialog();
    const queryClient = useQueryClient();

    const { mutateAsync: deleteCustomer } = useDeleteCustomer({
        mutation: { onSuccess: () => queryClient.invalidateQueries({ queryKey: getGetCustomersQueryKey() }) },
    });

    return (
        <DropdownMenu>
            <DropdownMenuTrigger asChild>
                <Button
                    variant='ghost'
                    className='data-[state=open]:bg-muted text-muted-foreground flex size-8 cursor-pointer'
                    size='icon'
                >
                    <EllipsisVerticalIcon />
                    <span className='sr-only'>Open menu</span>
                </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align='end' className='w-32'>
                <Can permission='customers:update'>
                    <DropdownMenuItem
                        onClick={async event => {
                            event.stopPropagation();
                            await modalDialog.open(props => (
                                <EditCustomerDialog {...props} customerId={row.original.id} />
                            ));
                        }}
                    >
                        Edit
                    </DropdownMenuItem>
                </Can>
                <Can permission='customers:delete'>
                    <DropdownMenuSeparator />
                    <DropdownMenuItem
                        variant='destructive'
                        onClick={async event => {
                            event.stopPropagation();
                            const confirmed = await modalDialog.open<boolean>(props => (
                                <ConfirmDialog
                                    {...props}
                                    title='Delete customer'
                                    description={`${row.original.fullName} will be permanently deleted and cannot be recovered.`}
                                    confirmLabel='Delete'
                                />
                            ));
                            if (confirmed) {
                                await deleteCustomer({ id: row.original.id });
                                toast.success(`${row.original.fullName} has been deleted.`);
                            }
                        }}
                    >
                        Delete
                    </DropdownMenuItem>
                </Can>
            </DropdownMenuContent>
        </DropdownMenu>
    );
};
