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
            <span class="text-2xl">{{ action.icon }}</span>
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

const router = useRouter()

const actions = ref([
  {
    title: 'Projects',
    description: 'View and manage your solutions and projects',
    icon: '📦',
    button: 'View Projects',
    action: () => router.push('/projects'),
  },
  {
    title: 'Build',
    description: 'Build solutions and projects',
    icon: '🔨',
    button: 'Open Builder',
    action: () => alert('Build feature coming soon!'),
  },
  {
    title: 'Run',
    description: 'Run projects and applications',
    icon: '▶️',
    button: 'Open Runner',
    action: () => alert('Run feature coming soon!'),
  },
  {
    title: 'Logs',
    description: 'View application logs',
    icon: '📋',
    button: 'View Logs',
    action: () => alert('Logs feature coming soon!'),
  },
  {
    title: 'Environment',
    description: 'Manage virtual environments',
    icon: '⚙️',
    button: 'Configure',
    action: () => alert('Environment feature coming soon!'),
  },
  {
    title: 'Local Sources',
    description: 'Manage local source mappings',
    icon: '🔗',
    button: 'Manage',
    action: () => alert('Local Sources feature coming soon!'),
  },
])
</script>
