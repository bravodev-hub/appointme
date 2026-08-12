import { AppLogo } from './app-logo';
import { navData } from './nav-data';
import { NavMain } from './nav-main';
import { NavUser } from './nav-user';
import {
    Sidebar,
    SidebarContent,
    SidebarFooter,
    SidebarHeader,
    SidebarMenu,
    SidebarMenuButton,
    SidebarMenuItem,
    SidebarSeparator,
    useSidebar,
} from '@/components/ui';
import { ComponentProps } from 'react';
import { Link } from 'react-router';

export const AppSidebar = ({ ...props }: ComponentProps<typeof Sidebar>) => {
    const { isMobile, setOpenMobile } = useSidebar();

    return (
        <Sidebar collapsible='icon' {...props}>
            <SidebarHeader>
                <SidebarMenu>
                    <SidebarMenuItem>
                        <SidebarMenuButton asChild className='data-[slot=sidebar-menu-button]:p-1.5!'>
                            <Link to='/' onClick={() => isMobile && setOpenMobile(false)}>
                                <AppLogo className='size-5.5! shrink-0' />
                                <span className='text-base'>
                                    Appoint<span className='font-semibold'>Me</span>
                                </span>
                            </Link>
                        </SidebarMenuButton>
                    </SidebarMenuItem>
                </SidebarMenu>
            </SidebarHeader>
            <SidebarContent>
                <NavMain items={navData.navMain} />
                <SidebarSeparator className='mx-0' />
                <NavMain items={navData.navSettings} label='Settings' />
            </SidebarContent>
            <SidebarFooter>
                <NavUser />
            </SidebarFooter>
        </Sidebar>
    );
};
