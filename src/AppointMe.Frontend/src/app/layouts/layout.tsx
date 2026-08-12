import { Footer } from './footer';
import { Header } from './header';
import { AppSidebar } from './sidebar';
import { SidebarInset, SidebarProvider, Toaster } from '@/components/ui';
import { Outlet } from 'react-router';

export const Layout = () => {
    return (
        <div className='flex h-dvh flex-col bg-gray-50'>
            <Toaster position='top-center' />
            <main className='flex-1'>
                <SidebarProvider>
                    <AppSidebar />
                    <SidebarInset>
                        <Header />
                        <div className='flex min-h-0 flex-1 flex-col gap-4 overflow-auto p-4'>
                            <Outlet />
                        </div>
                        <Footer />
                    </SidebarInset>
                </SidebarProvider>
            </main>
        </div>
    );
};
