<template>
  <div class="container mx-auto px-4 py-8">
    <div class="mb-8 flex items-center justify-between">
      <div>
        <h1 class="mb-2 text-3xl font-bold text-white">Project Explorer</h1>
        <p class="text-white/80">Browse projects, logs, and configurations</p>
      </div>
      <Button @click="router.back()" variant="outline">
        ← Back
      </Button>
    </div>

    <div class="grid gap-6 lg:grid-cols-2">
      <!-- Projects Tree -->
      <Card>
        <CardHeader>
          <CardTitle class="flex items-center gap-2">
            <FolderKanban class="h-5 w-5" />
            Projects
          </CardTitle>
          <CardDescription>Solutions and projects</CardDescription>
        </CardHeader>
        <CardContent>
          <Tree>
            <TreeItem
              v-for="project in projectData"
              :key="project.value"
              :item="project"
              :icon="FolderIcon"
              label="project.label"
              :value="project.value"
            >
              <template #children>
                <TreeItem
                  v-for="child in project.children"
                  :key="child.value"
                  :icon="FileIcon"
                  label="child.label"
                  :value="child.value"
                  class="ml-2"
                />
              </template>
            </TreeItem>
          </Tree>
        </CardContent>
      </Card>

      <!-- Logs Tree -->
      <Card>
        <CardHeader>
          <CardTitle class="flex items-center gap-2">
            <FileText class="h-5 w-5" />
            Logs
          </CardTitle>
          <CardDescription>Application logs</CardDescription>
        </CardHeader>
        <CardContent>
          <Tree>
            <TreeItem
              v-for="log in logData"
              :key="log.value"
              :item="log"
              :icon="FolderIcon"
              label="log.label"
              :value="log.value"
            >
              <template #children>
                <TreeItem
                  v-for="file in log.children"
                  :key="file.value"
                  :icon="FileTextIcon"
                  label="file.label"
                  :value="file.value"
                  class="ml-2"
                />
              </template>
            </TreeItem>
          </Tree>
        </CardContent>
      </Card>

      <!-- Environment Variables Tree -->
      <Card>
        <CardHeader>
          <CardTitle class="flex items-center gap-2">
            <Settings2 class="h-5 w-5" />
            Environment
          </CardTitle>
          <CardDescription>Environment configurations</CardDescription>
        </CardHeader>
        <CardContent>
          <Tree>
            <TreeItem
              v-for="env in envData"
              :key="env.value"
              :item="env"
              :icon="FolderIcon"
              label="env.label"
              :value="env.value"
            >
              <template #children>
                <TreeItem
                  v-for="variable in env.children"
                  :key="variable.value"
                  :icon="VariableIcon"
                  label="variable.label"
                  :value="variable.value"
                  class="ml-2"
                />
              </template>
            </TreeItem>
          </Tree>
        </CardContent>
      </Card>

      <!-- Local Sources Tree -->
      <Card>
        <CardHeader>
          <CardTitle class="flex items-center gap-2">
            <GitBranch class="h-5 w-5" />
            Local Sources
          </CardTitle>
          <CardDescription>Source package mappings</CardDescription>
        </CardHeader>
        <CardContent>
          <Tree>
            <TreeItem
              v-for="source in sourceData"
              :key="source.value"
              :item="source"
              :icon="GitRepoIcon"
              label="source.label"
              :value="source.value"
            >
              <template #children>
                <TreeItem
                  v-for="pkg in source.children"
                  :key="pkg.value"
                  :icon="PackageIcon"
                  label="pkg.label"
                  :value="pkg.value"
                  class="ml-2"
                />
              </template>
            </TreeItem>
          </Tree>
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
import Tree from '@/components/ui/Tree.vue'
import TreeItem from '@/components/ui/TreeItem.vue'
import {
  Folder,
  File,
  FileText,
  Settings2,
  GitBranch,
  Package as PackageIcon,
  FolderKanban,
} from 'lucide-vue-next'

const router = useRouter()

interface TreeNode {
  label: string
  value: string
  children?: TreeNode[]
}

const projectData = ref<TreeNode[]>([
  {
    label: 'AbpDevTools',
    value: 'abpdevtools',
    children: [
      { label: 'AbpDevTools.sln', value: 'abpdevtools-sln' },
      { label: 'AbpDevTools.csproj', value: 'abpdevtools-csproj' },
      { label: 'AbpDevTools.Tests.csproj', value: 'abpdevtools-tests-csproj' },
    ],
  },
  {
    label: 'Other Projects',
    value: 'other-projects',
    children: [
      { label: 'MySolution.sln', value: 'my-solution' },
      { label: 'WebApplication.csproj', value: 'web-app' },
      { label: 'HttpApi.Host.csproj', value: 'http-api-host' },
    ],
  },
])

const logData = ref<TreeNode[]>([
  {
    label: 'MyApp.Web',
    value: 'myapp-web',
    children: [
      { label: '2025-01-21.log', value: 'log-2025-01-21' },
      { label: '2025-01-20.log', value: 'log-2025-01-20' },
      { label: '2025-01-19.log', value: 'log-2025-01-19' },
    ],
  },
  {
    label: 'MyApp.HttpApi.Host',
    value: 'myapp-api',
    children: [
      { label: '2025-01-21.log', value: 'api-log-2025-01-21' },
      { label: '2025-01-20.log', value: 'api-log-2025-01-20' },
    ],
  },
])

const envData = ref<TreeNode[]>([
  {
    label: 'SqlServer',
    value: 'sqlserver',
    children: [
      { label: 'ConnectionStrings__Default', value: 'sqlserver-connection' },
      { label: 'ConnectionStrings__AbpCommercial', value: 'sqlserver-abp' },
      { label: 'ASPNETCORE_URL', value: 'sqlserver-url' },
    ],
  },
  {
    label: 'MongoDB',
    value: 'mongodb',
    children: [
      { label: 'ConnectionStrings__Default', value: 'mongodb-connection' },
      { label: 'MongoSettings__Database', value: 'mongodb-db' },
    ],
  },
])

const sourceData = ref<TreeNode[]>([
  {
    label: 'ABP Framework',
    value: 'abp-framework',
    children: [
      { label: 'Volo.Abp.Core', value: 'volo-abp-core' },
      { label: 'Volo.Abp.EntityFrameworkCore', value: 'volo-abp-efcore' },
      { label: 'Volo.Abp.AspNetCore', value: 'volo-abp-aspnetcore' },
    ],
  },
  {
    label: 'Other Libraries',
    value: 'other-libs',
    children: [
      { label: 'MyOrg.Common', value: 'myorg-common' },
      { label: 'MyOrg.Security', value: 'myorg-security' },
    ],
  },
])

const FolderIcon = Folder
const FileIcon = File
const FileTextIcon = FileText
const VariableIcon = Settings2
const GitRepoIcon = GitBranch
</script>

