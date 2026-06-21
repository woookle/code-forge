import { useEffect, useState, useCallback } from 'react';
import { useAppSelector, useAppDispatch } from './app/hooks';
import { checkAuth } from './features/auth/authSlice';
import Login from './components/auth/Login';
import Dashboard from './components/dashboard/Dashboard';
import Profile from './components/profile/Profile';
import AdminPanel from './components/admin/AdminPanel';
import SplashScreen from './components/common/SplashScreen';
import { ToastContainer, toast } from 'react-toastify';
import 'react-toastify/dist/ReactToastify.css';
import { ClipLoader } from 'react-spinners';

export type Page = 'dashboard' | 'profile' | 'admin';

const SPLASH_KEY = 'codeforge_visited_v2';

function App() {
    const dispatch = useAppDispatch();
    const { user, isAuthenticated, loading } = useAppSelector((state) => state.auth);
    const [currentPage, setCurrentPage] = useState<Page>('dashboard');
    const [showSplash, setShowSplash] = useState<boolean>(() => !sessionStorage.getItem(SPLASH_KEY));

    const handleSplashDone = useCallback(() => {
        sessionStorage.setItem(SPLASH_KEY, '1');
        setShowSplash(false);
    }, []);

    useEffect(() => {
        // Проверяем аутентификацию при загрузке приложения
        dispatch(checkAuth());
    }, [dispatch]);

    // Перенаправление при попытке открыть admin без соответствующей роли
    useEffect(() => {
        if (currentPage === 'admin' && user?.role !== 'Admin') {
            setCurrentPage('dashboard');
            toast.error('Доступ запрещен');
        }
    }, [currentPage, user]);

    // Применяем тёмную тему
    useEffect(() => {
        if (user?.isDarkMode) {
            document.body.classList.add('dark-mode');
        } else {
            document.body.classList.remove('dark-mode');
        }
    }, [user?.isDarkMode]);

    return (
        <>
            {/* Заставка только при первом посещении */}
            {showSplash && <SplashScreen onDone={handleSplashDone} />}

            <ToastContainer position="bottom-left" stacked={true} autoClose={3000} hideProgressBar={false} newestOnTop closeOnClick rtl={false} pauseOnFocusLoss draggable pauseOnHover theme={user?.isDarkMode ? "dark" : "light"} />

            {loading ? (
                <div style={{
                    display: 'flex',
                    flexDirection: 'column',
                    justifyContent: 'center',
                    alignItems: 'center',
                    minHeight: '100vh',
                    background: 'linear-gradient(135deg, #f1f5ff 0%, #f5f3ff 100%)',
                    gap: '1.5rem'
                }}>
                    <div style={{
                        width: 56, height: 56,
                        borderRadius: '16px',
                        background: 'linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%)',
                        display: 'flex', alignItems: 'center', justifyContent: 'center',
                        fontSize: '1.6rem',
                        boxShadow: '0 8px 24px rgba(99,102,241,0.35)'
                    }}>⚡</div>
                    <ClipLoader color="#6366f1" size={32} />
                    <p style={{ color: '#64748b', fontSize: '0.9rem', fontWeight: 500, margin: 0 }}>CodeForge загружается...</p>
                </div>
            ) : !isAuthenticated ? (
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
