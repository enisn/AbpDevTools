<template>
  <div :class="cn('relative', props.class)">
    <ul role="tree" :class="cn('m-0 list-none', listClass)">
      <slot />
    </ul>
  </div>
</template>

<script setup lang="ts">
import type { HTMLAttributes } from 'vue'
import { provide, ref } from 'vue'
import { cn } from '@/lib/utils'

interface Props {
  class?: HTMLAttributes['class']
  listClass?: HTMLAttributes['class']
}

const props = withDefaults(defineProps<Props>(), {
  listClass: '',
})

const expandedKeys = ref<Set<string>>(new Set())

provide('expandedKeys', expandedKeys)
provide('toggleExpanded', (key: string) => {
  if (expandedKeys.value.has(key)) {
    expandedKeys.value.delete(key)
  } else {
    expandedKeys.value.add(key)
  }
})

provide('isExpanded', (key: string) => expandedKeys.value.has(key))
</script>
