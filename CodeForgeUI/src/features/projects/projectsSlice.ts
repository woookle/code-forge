import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import api from '../../utils/api';
import { Project } from '../../types';
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
                state.error = action.error.message || 'Failed to fetch projects';
            })
            .addCase(fetchProjectById.fulfilled, (state, action) => {
                state.currentProject = action.payload;
            })
            .addCase(createProject.fulfilled, (state, action) => {
                state.projects.push(action.payload);
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
