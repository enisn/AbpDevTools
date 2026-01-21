<template>
  <div class="container mx-auto px-4 py-8">
    <div class="mb-8 text-center">
      <h1 class="mb-2 text-4xl font-bold text-white">🚀 AbpDevTools</h1>
      <p class="text-lg text-white/80">Web UI for ABP Development Tools</p>
    </div>

    <div class="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
      <Card v-for="action in actions" :key="action.title" class="hover:shadow-lg transition-shadow">
        <CardHeader>
          <CardTitle class="flex items-center gap-2">
            <component :is="action.icon" class="h-6 w-6 text-primary" />
            {{ action.title }}
          </CardTitle>
          <CardDescription>{{ action.description }}</CardDescription>
        </CardHeader>
        <CardContent>
          <Button @click="action.action" class="w-full">
            {{ action.button }}
          </Button>
        </CardContent>
      </Card>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import Card from '@/components/ui/Card.vue'
import CardHeader from '@/components/ui/CardHeader.vue'
import CardTitle from '@/components/ui/CardTitle.vue'
import CardDescription from '@/components/ui/CardDescription.vue'
import CardContent from '@/components/ui/CardContent.vue'
import Button from '@/components/ui/Button.vue'
import {
  FolderKanban as ProjectsIcon,
  Hammer as BuildIcon,
  Play as RunIcon,
  FileText as LogsIcon,
  Settings as EnvironmentIcon,
  GitBranch as SourcesIcon,
  TreeDeciduous as ExplorerIcon,
} from 'lucide-vue-next'

const router = useRouter()

const actions = ref([
   {
     title: 'Projects',
     description: 'View and manage your solutions and projects',
     icon: ProjectsIcon,
     button: 'View Projects',
     action: () => router.push('/projects'),
   },
   {
     title: 'Build',
     description: 'Build solutions and projects',
     icon: BuildIcon,
     button: 'Open Builder',
     action: () => alert('Build feature coming soon!'),
   },
   {
     title: 'Run',
     description: 'Run projects and applications',
     icon: RunIcon,
     button: 'Open Runner',
     action: () => alert('Run feature coming soon!'),
   },
   {
     title: 'Logs',
     description: 'View application logs',
     icon: LogsIcon,
     button: 'View Logs',
     action: () => alert('Logs feature coming soon!'),
   },
   {
     title: 'Environment',
     description: 'Manage virtual environments',
     icon: EnvironmentIcon,
     button: 'Configure',
     action: () => alert('Environment feature coming soon!'),
   },
   {
     title: 'Local Sources',
     description: 'Manage local source mappings',
     icon: SourcesIcon,
     button: 'Manage',
     action: () => alert('Local Sources feature coming soon!'),
   },
   {
     title: 'Explorer',
     description: 'Hierarchical view of all resources',
     icon: ExplorerIcon,
     button: 'Open Explorer',
     action: () => router.push('/explorer'),
   },
 ])
</script>

