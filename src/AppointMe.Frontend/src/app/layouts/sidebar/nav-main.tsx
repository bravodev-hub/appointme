import { NavBadge } from './nav-badge';
import { NavItem } from './nav-data';
import { useActiveNavIds } from '@/app/router';
import { hasPermission, useCurrentUser } from '@/components/auth';
import { useUserAccess } from '@/components/auth/use-user-access';
import {
    SidebarGroup,
    SidebarGroupContent,
    SidebarGroupLabel,
    SidebarMenu,
    SidebarMenuButton,
    SidebarMenuItem,
    SidebarSeparator,
    useSidebar,
} from '@/components/ui';
import { Link } from 'react-router';

interface NavMainProps {
    items: NavItem[];
    label?: string;
    /** Draws a divider above the group. Omitted for the first group in the sidebar. */
    separated?: boolean;
}

export const NavMain = ({ items, label, separated }: NavMainProps) => {
    const activeNavIds = useActiveNavIds();
    const { permissions } = useUserAccess();
    const currentUser = useCurrentUser();
    const { isMobile, setOpenMobile } = useSidebar();

    const visibleItems = items.filter(
        item =>
            (!item.permission || hasPermission(permissions, item.permission)) &&
            (!item.superAdminOnly || currentUser.isSuperAdmin),
    );

    // A group whose every entry is gated away renders nothing — no empty label or divider.
    if (visibleItems.length === 0) {
        return null;
    }

    return (
        <>
            {separated && <SidebarSeparator className='mx-0' />}
            <SidebarGroup>
                {label && <SidebarGroupLabel>{label}</SidebarGroupLabel>}
                <SidebarGroupContent className='flex flex-col gap-2'>
                    <SidebarMenu>
                        {visibleItems.map(item => {
                            const closeOnMobile = () => isMobile && setOpenMobile(false);
                            const content = (
                                <>
                                    {item.icon && <item.icon />}
                                    <span>{item.title}</span>
                                    {item.badge && <NavBadge label={item.badge} />}
                                </>
                            );

                            return (
                                <SidebarMenuItem key={item.title}>
                                    <SidebarMenuButton
                                        tooltip={item.title}
                                        asChild
                                        isActive={activeNavIds.has(item.navId)}
                                    >
                                        {item.external ? (
                                            <a href={item.url} target='_blank' rel='noreferrer' onClick={closeOnMobile}>
                                                {content}
                                            </a>
                                        ) : (
                                            <Link to={item.url} onClick={closeOnMobile}>
                                                {content}
                                            </Link>
                                        )}
                                    </SidebarMenuButton>
                                </SidebarMenuItem>
                            );
                        })}
                    </SidebarMenu>
                </SidebarGroupContent>
            </SidebarGroup>
        </>
    );
};
