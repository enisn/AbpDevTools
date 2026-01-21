import { createRouter, createWebHistory } from 'vue-router'
import Dashboard from '@/views/Dashboard.vue'
import Projects from '@/views/Projects.vue'
import Explorer from '@/views/Explorer.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'Dashboard',
      component: Dashboard,
    },
    {
      path: '/projects',
      name: 'Projects',
      component: Projects,
    },
    {
      path: '/explorer',
      name: 'Explorer',
      component: Explorer,
    },
  ],
})

export default router
