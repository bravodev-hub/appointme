import { ActionsCell } from './actions-cell';
import { TeamMemberDto, TeamMemberType } from '@/api/appointme.schemas.ts';
import {
    Avatar,
    AvatarFallback,
    Badge,
    Checkbox,
    } from '@/components/ui';
import { ColumnDef } from '@tanstack/react-table';

export const Columns: ColumnDef<TeamMemberDto>[] = [
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
            <div className='flex flex-wrap items-center gap-2'>
                {row.original.type === TeamMemberType.Employee && (
                    <>
                        <Avatar>
                            <AvatarFallback>{row.original.initials}</AvatarFallback>
                        </Avatar>
                        <div className='flex flex-col'>
                            <span>{row.getValue('fullName')}</span>
                            <span className='text-muted-foreground text-xs break-all md:hidden'>
                                {row.original.email}
                            </span>
                        </div>
                    </>
                )}
                {row.original.type === TeamMemberType.Invitation && (
                    <div className='flex flex-col'>
                        <Badge variant='outline'>Pending invite</Badge>
                        <span className='text-muted-foreground mt-1 text-xs break-all md:hidden'>
                            {row.original.email}
                        </span>
                    </div>
                )}
                {row.original.isCurrentUser && <Badge variant='outline'>You</Badge>}
                {row.original.isPrimaryOwner && <Badge variant='outline'>Owner</Badge>}
            </div>
        ),
        enableHiding: false,
        meta: {
            className: 'whitespace-normal md:whitespace-nowrap',
        },
    },
    {
        accessorKey: 'email',
        header: 'Email',
        cell: ({ row }) => <div>{row.getValue('email')}</div>,
        meta: {
            className: 'hidden md:table-cell',
        },
    },
    {
        accessorKey: 'roles',
        header: 'Roles',
        cell: ({ row }) => (
            <div className='flex gap-1'>
                {row.original.roles.map(role => (
                    <Badge key={role} variant='secondary'>
                        {role}
                    </Badge>
                ))}
            </div>
        ),
        enableSorting: false,
        meta: {
            className: 'hidden lg:table-cell',
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
