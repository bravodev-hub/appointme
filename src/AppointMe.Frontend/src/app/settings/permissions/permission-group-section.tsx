import { PermissionCheckboxCell } from './permission-checkbox-cell.tsx';
import { labelForPermission, labelForResource } from './permission-labels.ts';
import { PermissionGroupDto, RoleDefinitionDto } from '@/api/client.schemas.ts';
import { TableBody, TableCell, TableRow } from '@/components/ui';

interface PermissionGroupSectionProps {
    group: PermissionGroupDto;
    roles: RoleDefinitionDto[];
}

export const PermissionGroupSection = ({ group, roles }: Readonly<PermissionGroupSectionProps>) => (
    <TableBody>
        <TableRow className='bg-muted/10 hover:bg-muted/10'>
            <TableCell colSpan={roles.length + 1} className='text-muted-foreground py-2 text-xs font-medium'>
                <span className='sticky left-2 inline-block px-4'>{labelForResource(group.name)}</span>
            </TableCell>
        </TableRow>
        {group.permissions.map(permission => {
            const label = labelForPermission(permission.key);
            return (
                <TableRow key={permission.key}>
                    <TableCell className='bg-background sticky left-0 z-10 w-72 pl-8'>{label}</TableCell>
                    {roles.map(roleDto => (
                        <PermissionCheckboxCell
                            key={roleDto.role}
                            permission={permission}
                            role={roleDto}
                            label={label}
                        />
                    ))}
                </TableRow>
            );
        })}
    </TableBody>
);
