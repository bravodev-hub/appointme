import { PermissionCellState, usePermissionEditorContext } from './use-permission-editor-context.ts';
import { PermissionDto, RoleDefinitionDto } from '@/api/appointme.schemas.ts';
import { Checkbox, TableCell, Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui';
import { cn } from '@/lib/utils.ts';

interface PermissionCheckboxCellProps {
    permission: PermissionDto;
    role: RoleDefinitionDto;
    label: string;
}

const GRANT_STATE_LABEL: Record<PermissionCellState['state'], string> = {
    default: 'Inherited',
    overridden: 'Overridden',
    modified: 'Modified',
};

export const PermissionCheckboxCell = ({ permission, role, label }: Readonly<PermissionCheckboxCellProps>) => {
    const { toggle, getCellState } = usePermissionEditorContext();
    const { isGranted, isEditable, state } = getCellState(permission, role);

    return (
        <TableCell className='min-w-32.5 text-center'>
            <Tooltip>
                <TooltipTrigger asChild>
                    <div className='relative mx-auto inline-flex'>
                        <Checkbox
                            checked={isGranted}
                            disabled={!isEditable}
                            onCheckedChange={value => toggle(permission.key, role.role, Boolean(value))}
                            aria-label={
                                state === 'default'
                                    ? `${role.role}: ${label}`
                                    : `${role.role}: ${label}, ${GRANT_STATE_LABEL[state]}`
                            }
                            className={cn(state === 'overridden' && 'ring-2 ring-amber-400 ring-offset-1')}
                        />
                        {state === 'modified' && (
                            <span className='bg-primary absolute -top-1 -right-1 size-2 rounded-full' />
                        )}
                    </div>
                </TooltipTrigger>
                {isEditable && <TooltipContent>{GRANT_STATE_LABEL[state]}</TooltipContent>}
            </Tooltip>
        </TableCell>
    );
};
