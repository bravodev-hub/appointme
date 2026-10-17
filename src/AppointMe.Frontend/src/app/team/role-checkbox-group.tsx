import { EMPLOYEE_ROLES } from './roles';
import { Role } from '@/api/client.schemas.ts';
import { Checkbox } from '@/components/ui';
import { Label } from '@/components/ui/label';

interface RoleCheckboxGroupProps {
    value: Role[];
    onChange: (roles: Role[]) => void;
    isRoleDisabled?: (role: Role) => boolean;
}

export const RoleCheckboxGroup = ({ value, onChange, isRoleDisabled }: RoleCheckboxGroupProps) => (
    <div className='flex flex-col gap-3'>
        {EMPLOYEE_ROLES.map(role => (
            <Label key={role.value} className='flex items-start gap-3 font-normal'>
                <Checkbox
                    checked={value.includes(role.value)}
                    disabled={isRoleDisabled?.(role.value)}
                    onCheckedChange={checked => {
                        onChange(checked ? [...value, role.value] : value.filter(r => r !== role.value));
                    }}
                />
                <div className='flex flex-col gap-0.5'>
                    <span className='text-sm leading-none font-medium'>{role.value}</span>
                    <span className='text-muted-foreground text-xs'>{role.description}</span>
                </div>
            </Label>
        ))}
    </div>
);
