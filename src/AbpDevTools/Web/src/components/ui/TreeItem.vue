<template>
  <li
    :class="cn('flex flex-col', props.class)"
    :data-state="checkExpanded(itemKey) ? 'open' : 'closed'"
  >
    <div
      :class="cn('flex items-center gap-2 py-1.5 px-2 rounded-md hover:bg-accent/50 cursor-pointer', indentClass)"
      @click="handleToggle"
    >
      <span v-if="hasChildren" class="flex h-4 w-4 items-center justify-center">
        <ChevronRight
          :class="cn('h-4 w-4 transition-transform', checkExpanded(itemKey) ? 'rotate-90' : '')"
        />
      </span>
      <span v-else class="flex h-4 w-4"></span>
      <component :is="icon" v-if="icon" :class="iconClass" />
      <span class="flex-1 text-sm">{{ label }}</span>
      <slot name="actions" :item="item" :expanded="checkExpanded(itemKey)" />
    </div>
    <Collapsible v-if="hasChildren && checkExpanded(itemKey)">
      <ul class="ml-4 mt-1 list-none">
        <slot name="children" />
      </ul>
    </Collapsible>
  </li>
</template>

<script setup lang="ts">
import type { HTMLAttributes } from 'vue'
import { computed, inject } from 'vue'
import { ChevronRight } from 'lucide-vue-next'
import Collapsible from '@/components/ui/Collapsible.vue'
import { cn } from '@/lib/utils'

interface TreeNode {
  children?: any[]
}

interface Props {
  label: string
  value: string
  icon?: any
  iconClass?: string
  class?: HTMLAttributes['class']
  indentClass?: string
  item?: TreeNode
}

const props = withDefaults(defineProps<Props>(), {
  iconClass: 'h-4 w-4',
  indentClass: '',
  item: undefined,
})

const toggleExpanded = inject<(key: string) => void>('toggleExpanded') as (key: string) => void
const isExpanded = inject<(key: string) => boolean>('isExpanded') as (key: string) => boolean

const hasChildren = computed(() => props.item && props.item.children && props.item.children.length > 0)
const itemKey = computed(() => props.value || props.label)

function handleToggle() {
  toggleExpanded(itemKey.value)
}

function checkExpanded(key: string) {
  return isExpanded(key)
}
</script>



