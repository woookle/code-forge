import { useEffect, useState } from 'react';
import { useAppSelector, useAppDispatch } from './app/hooks';
import { checkAuth } from './features/auth/authSlice';
import Login from './components/Login';
import Dashboard from './components/Dashboard';
import Profile from './components/Profile';
import AdminPanel from './components/AdminPanel';
import { ToastContainer, toast } from 'react-toastify';
import 'react-toastify/dist/ReactToastify.css';
import { ClipLoader } from 'react-spinners';

export type Page = 'dashboard' | 'profile' | 'admin';

function App() {
    const dispatch = useAppDispatch();
    const { user, isAuthenticated, loading } = useAppSelector((state) => state.auth);
    const [currentPage, setCurrentPage] = useState<Page>('dashboard');

    useEffect(() => {
        // Check if user is authenticated on app load
        dispatch(checkAuth());
    }, [dispatch]);

    // Redirect or show not found if trying to access admin without role
    useEffect(() => {
        if (currentPage === 'admin' && user?.role !== 'Admin') {
            setCurrentPage('dashboard');
            toast.error('Доступ запрещен');
        }
    }, [currentPage, user]);

    // Apply dark mode
    useEffect(() => {
        if (user?.isDarkMode) {
            document.body.classList.add('dark-mode');
        } else {
            document.body.classList.remove('dark-mode');
        }
    }, [user?.isDarkMode]);

    if (loading) {
        return <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '100vh', backgroundColor: '#f9fafb' }}>
            <ClipLoader color="#000000" size={50} />
        </div>;
    }

    return (
        <>
            <ToastContainer position="bottom-left" stacked={true} autoClose={3000} hideProgressBar={false} newestOnTop closeOnClick rtl={false} pauseOnFocusLoss draggable pauseOnHover theme={user?.isDarkMode ? "dark" : "light"} />
            {!isAuthenticated ? (
                <Login />
            ) : (
                <>
                    {currentPage === 'dashboard' && <Dashboard onNavigate={setCurrentPage} />}
                    {currentPage === 'profile' && <Profile onNavigate={setCurrentPage} />}
                    {currentPage === 'admin' && user?.role === 'Admin' && <AdminPanel onNavigate={setCurrentPage} />}
                </>
            )}
        </>
    );
}

export default App;
