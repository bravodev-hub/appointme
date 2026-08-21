import { Badge } from '@/components/ui';

export const NavBadge = ({ label }: { label: string }) => (
    <Badge variant='outline' className='text-muted-foreground ml-auto px-1.5 py-0 text-[10px] tracking-wide'>
        {label}
    </Badge>
);

export const ProBadge = () => <NavBadge label='PRO' />;
