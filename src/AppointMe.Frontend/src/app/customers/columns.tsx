import { ActionsCell } from './actions-cell';
import { CustomerDto } from '@/api/appointme.schemas';
import { FormattedDate } from '@/components/format';
import {
    Avatar,
    AvatarFallback,
    Checkbox,
    } from '@/components/ui';
import { ColumnDef } from '@tanstack/react-table';

export const Columns: ColumnDef<CustomerDto>[] = [
    {
        id: 'select',
        header: ({ table }) => (
            <Checkbox
                checked={table.getIsAllPageRowsSelected() || (table.getIsSomePageRowsSelected() && 'indeterminate')}
                onCheckedChange={value => table.toggleAllPageRowsSelected(!!value)}
                aria-label='Select all'
            />
        ),
        cell: ({ row }) => (
            <Checkbox
                checked={row.getIsSelected()}
                disabled={!row.getCanSelect()}
                onCheckedChange={value => row.toggleSelected(!!value)}
                aria-label='Select row'
                onClick={e => e.stopPropagation()}
            />
        ),
        enableSorting: false,
        enableHiding: false,
        meta: {
            className: 'w-0',
        },
    },
    {
        accessorKey: 'fullName',
        header: 'Name',
        cell: ({ row }) => (
            <div className='flex items-center gap-2'>
                <Avatar>
                    <AvatarFallback>{row.original.initials}</AvatarFallback>
                </Avatar>
                <div className='flex flex-col'>
                    <span>{row.getValue('fullName')}</span>
                    {row.original.email && (
                        <span className='text-muted-foreground text-xs break-all lowercase md:hidden'>
                            {row.original.email}
                        </span>
                    )}
                </div>
            </div>
        ),
        enableHiding: false,
        meta: {
            className: 'whitespace-normal md:whitespace-nowrap',
        },
    },
    {
        accessorKey: 'dateOfBirth',
        header: 'Date of Birth',
        cell: ({ row }) =>
            row.original.dateOfBirth ? (
                <FormattedDate date={row.getValue('dateOfBirth')} format='dayMonthShortYear' />
            ) : null,
        meta: {
            className: 'hidden lg:table-cell',
        },
    },
    {
        accessorKey: 'email',
        header: 'Email',
        cell: ({ row }) => <div className='lowercase'>{row.getValue('email')}</div>,
        meta: {
            className: 'hidden md:table-cell',
        },
    },
    {
        id: 'actions',
        enableResizing: false,
        enableSorting: false,
        enableHiding: false,
        meta: {
            className: 'w-0',
        },
        cell: ({ row }) => <ActionsCell row={row} />,
    },
];
