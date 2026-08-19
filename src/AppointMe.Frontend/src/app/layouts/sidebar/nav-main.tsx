import { NavItem } from './nav-data';
import { useActiveNavIds } from '@/app/router';
import { hasPermission } from '@/components/auth';
import { useUserAccess } from '@/components/auth/use-user-access';
import {
    SidebarGroup,
    SidebarGroupContent,
    SidebarGroupLabel,
    SidebarMenu,
    SidebarMenuButton,
    SidebarMenuItem,
    useSidebar,
} from '@/components/ui';
import { Link } from 'react-router';

interface NavMainProps {
    items: NavItem[];
    label?: string;
}

export const NavMain = ({ items, label }: NavMainProps) => {
    const activeNavIds = useActiveNavIds();
    const { permissions } = useUserAccess();
    const { isMobile, setOpenMobile } = useSidebar();

    return (
        <SidebarGroup>
            {label && <SidebarGroupLabel>{label}</SidebarGroupLabel>}
            <SidebarGroupContent className='flex flex-col gap-2'>
                <SidebarMenu>
                    {items
                        .filter(item => !item.permission || hasPermission(permissions, item.permission))
                        .map(item => {
                            const content = (
                                <>
                                    {item.icon && <item.icon />}
                                    <span>{item.title}</span>
                                </>
                            );

                            return (
                                <SidebarMenuItem key={item.title}>
                                    <SidebarMenuButton
                                        tooltip={item.title}
                                        asChild
                                        isActive={activeNavIds.has(item.navId)}
                                    >
                                        <Link to={item.url} onClick={() => isMobile && setOpenMobile(false)}>
                                            {content}
                                        </Link>
                                    </SidebarMenuButton>
                                </SidebarMenuItem>
                            );
                        })}
                </SidebarMenu>
            </SidebarGroupContent>
        </SidebarGroup>
    );
};
