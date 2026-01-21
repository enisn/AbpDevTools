<template>
  <div class="container mx-auto px-4 py-8">
    <div class="mb-8 flex items-center justify-between">
      <div>
        <h1 class="mb-2 text-3xl font-bold text-white">Projects</h1>
        <p class="text-white/80">Manage your solutions and projects</p>
      </div>
      <Button @click="router.back()" variant="outline">
        ← Back
      </Button>
    </div>

    <div v-if="loading" class="flex items-center justify-center py-12">
      <div class="text-xl text-white">Loading projects...</div>
    </div>

    <div v-else-if="error" class="rounded-lg bg-red-500 p-4 text-white">
      {{ error }}
    </div>

    <div v-else-if="projects.length === 0" class="flex flex-col items-center justify-center py-12 text-white">
      <span class="mb-4 text-6xl">📂</span>
      <p class="text-xl">No projects found in current directory</p>
    </div>

    <div v-else class="space-y-4">
      <Card v-for="project in projects" :key="project.path" class="cursor-pointer hover:shadow-lg transition-shadow" @click="selectProject(project)">
        <CardContent class="flex items-center gap-4 py-6">
          <div class="flex h-12 w-12 items-center justify-center rounded-xl text-2xl" :class="getProjectIconClass(project.type)">
            {{ project.type === 'solution' ? '📦' : '📄' }}
          </div>
          <div class="flex-1">
            <h3 class="text-lg font-semibold">{{ project.name }}</h3>
            <p class="text-sm text-muted-foreground font-mono">{{ project.path }}</p>
          </div>
          <span class="rounded-full bg-secondary px-3 py-1 text-sm font-medium text-secondary-foreground">
            {{ project.type }}
          </span>
        </CardContent>
      </Card>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import Card from '@/components/ui/Card.vue'
import CardContent from '@/components/ui/CardContent.vue'
import Button from '@/components/ui/Button.vue'

const router = useRouter()

interface Project {
  name: string
  path: string
  type: 'solution' | 'project'
}

const projects = ref<Project[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

onMounted(async () => {
  await loadProjects()
})

async function loadProjects() {
  loading.value = true
  error.value = null
  
  try {
    const response = await fetch('/api/projects')
    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`)
    }
    projects.value = await response.json()
  } catch (e) {
    error.value = 'Failed to load projects: ' + (e as Error).message
    console.error(e)
  } finally {
    loading.value = false
  }
}

function selectProject(project: Project) {
  console.log('Selected:', project)
  alert(`Selected: ${project.name}\nType: ${project.type}\n\nMore features coming soon!`)
}

function getProjectIconClass(type: string) {
  return type === 'solution'
    ? 'bg-gradient-to-br from-blue-500 to-purple-600'
    : 'bg-gradient-to-br from-pink-500 to-red-600'
}
</script>
