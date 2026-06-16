import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import api from '../../utils/api';
import { Project, AuthConfig } from '../../types';
import { logout } from '../auth/authSlice';

interface ProjectsState {
    projects: Project[];
    currentProject: Project | null;
    loading: boolean;
    error: string | null;
}

const initialState: ProjectsState = {
    projects: [],
    currentProject: null,
    loading: false,
    error: null,
};

export const fetchProjects = createAsyncThunk('projects/fetchAll', async () => {
    const response = await api.get<Project[]>('/projects');
    return response.data;
});

export const fetchProjectById = createAsyncThunk('projects/fetchById', async (id: string) => {
    const response = await api.get<Project>(`/projects/${id}`);
    return response.data;
});

export const createProject = createAsyncThunk('projects/create', async (project: Partial<Project>) => {
    const response = await api.post<Project>('/projects', project);
    return response.data;
});

export const updateProject = createAsyncThunk(
    'projects/update',
    async ({ id, data }: { id: string; data: Partial<Project> }) => {
        await api.put(`/projects/${id}`, data);
        return { id, data };
    }
);

export const updateProjectAuth = createAsyncThunk(
    'projects/updateAuth',
    async ({ id, authConfig }: { id: string; authConfig: AuthConfig | null }) => {
        // Отправляем authConfig как объект — API сериализует его в JSON для хранения.
        // При отключении auth (authConfig === null) передаём clearAuth: true, чтобы
        // сервер явно обнулил поле (а не просто ничего не делал).
        await api.put(`/projects/${id}`, {
            authConfig: authConfig ?? undefined,
            clearAuth: authConfig === null,
        });
        // Сохраняем JSON-строку в Redux (тот же формат что возвращает сервер)
        const authConfigJson = authConfig ? JSON.stringify(authConfig) : null;
        return { id, authConfigJson };
    }
);

export const deleteProject = createAsyncThunk('projects/delete', async (id: string) => {
    await api.delete(`/projects/${id}`);
    return id;
});

export const generateProject = createAsyncThunk('projects/generate', async (id: string) => {
    const response = await api.post(`/projects/${id}/generate`, {}, { responseType: 'blob' });
    return response.data;
});

const projectsSlice = createSlice({
    name: 'projects',
    initialState,
    reducers: {
        setCurrentProject: (state, action) => {
            state.currentProject = action.payload;
        },
    },
    extraReducers: (builder) => {
        builder
            .addCase(fetchProjects.pending, (state) => {
                state.loading = true;
            })
            .addCase(fetchProjects.fulfilled, (state, action) => {
                state.loading = false;
                state.projects = action.payload;
            })
            .addCase(fetchProjects.rejected, (state, action) => {
                state.loading = false;
                state.error = action.error.message || 'Ошибка загрузки проектов';
            })
            .addCase(fetchProjectById.fulfilled, (state, action) => {
                state.currentProject = action.payload;
            })
            .addCase(createProject.fulfilled, (state, action) => {
                state.projects.push(action.payload);
            })
            .addCase(updateProjectAuth.fulfilled, (state, action) => {
                const { id, authConfigJson } = action.payload;
                if (state.currentProject?.id === id) {
                    state.currentProject = { ...state.currentProject, authConfig: authConfigJson };
                }
                const proj = state.projects.find(p => p.id === id);
                if (proj) proj.authConfig = authConfigJson;
            })
            .addCase(deleteProject.fulfilled, (state, action) => {
                state.projects = state.projects.filter(p => p.id !== action.payload);
                if (state.currentProject?.id === action.payload) {
                    state.currentProject = null;
                }
            })
            .addCase(logout.fulfilled, (state) => {
                state.projects = [];
                state.currentProject = null;
                state.error = null;
                state.loading = false;
            });
    },
});

export const { setCurrentProject } = projectsSlice.actions;
export default projectsSlice.reducer;
